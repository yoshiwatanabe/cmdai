using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class FileMemoryService : IMemoryService
{
    private readonly MemoryConfiguration _configuration;
    private readonly string _storePath;
    private readonly string _eventsPath;
    private readonly string _indexPath;
    private readonly string _machineId;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };
    private readonly List<MemoryEntry> _entries = [];
    private readonly Dictionary<string, HashSet<int>> _tokenIndex = new(StringComparer.OrdinalIgnoreCase);

    public FileMemoryService(MemoryConfiguration configuration)
    {
        _configuration = configuration;
        _machineId = BuildMachineId();
        _storePath = ResolveStorePath(configuration.StorePath);
        _eventsPath = Path.Combine(_storePath, "events");
        _indexPath = Path.Combine(_storePath, "index");

        Directory.CreateDirectory(_storePath);
        Directory.CreateDirectory(_eventsPath);
        Directory.CreateDirectory(_indexPath);

        TryMigrateLegacyLearningFile();
        LoadAll();
    }

    public Task<MemoryMatch?> FindBestMatchAsync(CommandRequest request)
    {
        lock (_lock)
        {
            if (_entries.Count == 0)
            {
                return Task.FromResult<MemoryMatch?>(null);
            }

            var queryTokens = Tokenize(request.Query).ToArray();
            if (queryTokens.Length == 0)
            {
                return Task.FromResult<MemoryMatch?>(null);
            }

            var candidateIndexes = GetCandidateIndexes(queryTokens);
            if (candidateIndexes.Count == 0)
            {
                return Task.FromResult<MemoryMatch?>(null);
            }

            var topCandidates = candidateIndexes.Take(_configuration.CandidateCap);
            MemoryEntry? bestEntry = null;
            var bestScore = 0.0;
            var bestReason = "No strong match";

            foreach (var index in topCandidates)
            {
                var entry = _entries[index];
                var score = Score(queryTokens, request, entry, out var reason);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestEntry = entry;
                    bestReason = reason;
                }
            }

            if (bestEntry == null)
            {
                return Task.FromResult<MemoryMatch?>(null);
            }

            var isHighConfidence = bestScore >= _configuration.HighConfidenceThreshold;
            return Task.FromResult<MemoryMatch?>(new MemoryMatch(bestEntry, bestScore, bestReason, isHighConfidence));
        }
    }

    public Task RecordAsync(CommandRequest request, CommandResult result, bool wasAccepted, bool wasSuccessful)
    {
        lock (_lock)
        {
            var sanitizedQuery = Redact(request.Query);
            var sanitizedCommand = Redact(result.Command);
            var normalizedTool = NormalizeTool(request.Tool, sanitizedCommand);
            var confidence = CalculateConfidence(wasAccepted, wasSuccessful);
            var hash = BuildContentHash(normalizedTool, sanitizedQuery, sanitizedCommand);

            var memoryEvent = new MemoryEvent(
                EventId: Guid.NewGuid().ToString("N"),
                MachineId: _machineId,
                TimestampUtc: DateTime.UtcNow,
                Tool: normalizedTool,
                Query: sanitizedQuery,
                Command: sanitizedCommand,
                WasAccepted: wasAccepted,
                WasSuccessful: wasSuccessful,
                ContentHash: hash,
                ConfidenceScore: confidence);

            AppendEvent(memoryEvent);
            UpsertEntry(memoryEvent);
            SaveSidecarIndex();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemoryEntry>> ListAsync(int? limit = null)
    {
        lock (_lock)
        {
            var take = limit ?? _configuration.ListLimitDefault;
            var list = _entries
                .OrderByDescending(e => e.TimestampUtc)
                .Take(Math.Max(1, take))
                .ToList();
            return Task.FromResult<IReadOnlyList<MemoryEntry>>(list);
        }
    }

    public Task ClearAsync()
    {
        lock (_lock)
        {
            foreach (var file in Directory.GetFiles(_eventsPath, "*.jsonl"))
            {
                File.Delete(file);
            }

            foreach (var file in Directory.GetFiles(_indexPath, "*.json"))
            {
                File.Delete(file);
            }

            _entries.Clear();
            _tokenIndex.Clear();
        }

        return Task.CompletedTask;
    }

    private static string ResolveStorePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cmdai", "memory");
    }

    private static string BuildMachineId()
    {
        var raw = $"{Environment.MachineName}|{Environment.UserName}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..16];
    }

    private void LoadAll()
    {
        var sidecarLoaded = TryLoadSidecarIndex();
        if (!sidecarLoaded)
        {
            RebuildFromEvents();
            SaveSidecarIndex();
        }
    }

    private bool TryLoadSidecarIndex()
    {
        var entriesFile = Path.Combine(_indexPath, "entries.json");
        var tokensFile = Path.Combine(_indexPath, "tokens.json");
        if (!File.Exists(entriesFile) || !File.Exists(tokensFile))
        {
            return false;
        }

        try
        {
            var loadedEntries = JsonSerializer.Deserialize<List<MemoryEntry>>(File.ReadAllText(entriesFile)) ?? [];
            var loadedTokenMap = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(File.ReadAllText(tokensFile))
                                 ?? new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            _entries.Clear();
            _entries.AddRange(loadedEntries);
            _tokenIndex.Clear();
            foreach (var pair in loadedTokenMap)
            {
                _tokenIndex[pair.Key] = pair.Value.ToHashSet();
            }

            return true;
        }
        catch
        {
            _entries.Clear();
            _tokenIndex.Clear();
            return false;
        }
    }

    private void RebuildFromEvents()
    {
        _entries.Clear();
        _tokenIndex.Clear();

        var dedup = new Dictionary<string, MemoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var eventFile in Directory.GetFiles(_eventsPath, "*.jsonl", SearchOption.TopDirectoryOnly))
        {
            foreach (var line in File.ReadLines(eventFile))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                MemoryEvent? memoryEvent;
                try
                {
                    memoryEvent = JsonSerializer.Deserialize<MemoryEvent>(line);
                }
                catch
                {
                    continue;
                }

                if (memoryEvent == null)
                {
                    continue;
                }

                var candidate = ToEntry(memoryEvent);
                if (dedup.TryGetValue(candidate.ContentHash, out var existing))
                {
                    if (candidate.ConfidenceScore > existing.ConfidenceScore ||
                        (Math.Abs(candidate.ConfidenceScore - existing.ConfidenceScore) < 0.0001 &&
                         candidate.TimestampUtc > existing.TimestampUtc))
                    {
                        dedup[candidate.ContentHash] = candidate;
                    }
                }
                else
                {
                    dedup[candidate.ContentHash] = candidate;
                }
            }
        }

        foreach (var entry in dedup.Values.OrderBy(e => e.TimestampUtc))
        {
            AddEntryAndIndex(entry);
        }
    }

    private void AppendEvent(MemoryEvent memoryEvent)
    {
        var logPath = Path.Combine(_eventsPath, $"{_machineId}.jsonl");
        var serialized = JsonSerializer.Serialize(memoryEvent, _jsonOptions);
        File.AppendAllText(logPath, serialized + Environment.NewLine);
    }

    private void UpsertEntry(MemoryEvent memoryEvent)
    {
        var entry = ToEntry(memoryEvent);
        var existingIndex = _entries.FindIndex(e => e.ContentHash == entry.ContentHash);
        if (existingIndex < 0)
        {
            AddEntryAndIndex(entry);
            return;
        }

        var existing = _entries[existingIndex];
        if (entry.ConfidenceScore > existing.ConfidenceScore || entry.TimestampUtc >= existing.TimestampUtc)
        {
            _entries[existingIndex] = entry;
            RebuildTokenIndex();
        }
    }

    private void AddEntryAndIndex(MemoryEntry entry)
    {
        var index = _entries.Count;
        _entries.Add(entry);
        foreach (var token in Tokenize(entry.Query))
        {
            if (!_tokenIndex.TryGetValue(token, out var posting))
            {
                posting = [];
                _tokenIndex[token] = posting;
            }

            posting.Add(index);
        }
    }

    private void RebuildTokenIndex()
    {
        _tokenIndex.Clear();
        for (var index = 0; index < _entries.Count; index++)
        {
            foreach (var token in Tokenize(_entries[index].Query))
            {
                if (!_tokenIndex.TryGetValue(token, out var posting))
                {
                    posting = [];
                    _tokenIndex[token] = posting;
                }

                posting.Add(index);
            }
        }
    }

    private void SaveSidecarIndex()
    {
        var entriesFile = Path.Combine(_indexPath, "entries.json");
        var tokensFile = Path.Combine(_indexPath, "tokens.json");
        File.WriteAllText(entriesFile, JsonSerializer.Serialize(_entries, _jsonOptions));
        var tokenMap = _tokenIndex.ToDictionary(k => k.Key, v => v.Value.OrderBy(x => x).ToList(), StringComparer.OrdinalIgnoreCase);
        File.WriteAllText(tokensFile, JsonSerializer.Serialize(tokenMap, _jsonOptions));
    }

    private List<int> GetCandidateIndexes(string[] queryTokens)
    {
        var candidateScores = new Dictionary<int, int>();
        foreach (var token in queryTokens)
        {
            if (!_tokenIndex.TryGetValue(token, out var postings))
            {
                continue;
            }

            foreach (var index in postings)
            {
                candidateScores[index] = candidateScores.TryGetValue(index, out var count) ? count + 1 : 1;
            }
        }

        if (candidateScores.Count == 0)
        {
            return [];
        }

        return candidateScores
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => _entries[pair.Key].TimestampUtc)
            .Select(pair => pair.Key)
            .ToList();
    }

    private static double Score(string[] queryTokens, CommandRequest request, MemoryEntry entry, out string reason)
    {
        var entryTokens = Tokenize(entry.Query).ToArray();
        var querySet = queryTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entrySet = entryTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var intersection = querySet.Intersect(entrySet, StringComparer.OrdinalIgnoreCase).Count();
        var union = querySet.Union(entrySet, StringComparer.OrdinalIgnoreCase).Count();
        var similarity = union == 0 ? 0 : (double)intersection / union;

        var successSignal = (entry.WasAccepted ? 0.5 : 0.0) + (entry.WasSuccessful ? 0.5 : 0.0);
        var toolBoost = 0.0;
        if (!string.IsNullOrWhiteSpace(request.Tool) &&
            !request.Tool.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
            entry.Tool.Equals(request.Tool, StringComparison.OrdinalIgnoreCase))
        {
            toolBoost = 0.08;
        }
        else if (request.Query.Contains(entry.Tool, StringComparison.OrdinalIgnoreCase))
        {
            toolBoost = 0.05;
        }

        var daysOld = Math.Max(0, (DateTime.UtcNow - entry.TimestampUtc).TotalDays);
        var recency = Math.Max(0.0, 1.0 - (daysOld / 365.0));

        var score = (similarity * 0.72) + (successSignal * 0.18) + toolBoost + (recency * 0.02) + (entry.ConfidenceScore * 0.08);
        reason = $"similarity={similarity:0.00}, success={successSignal:0.00}, toolBoost={toolBoost:0.00}";
        return Math.Min(1.0, score);
    }

    private static MemoryEntry ToEntry(MemoryEvent memoryEvent)
    {
        return new MemoryEntry(
            EntryId: memoryEvent.EventId,
            Tool: memoryEvent.Tool,
            Query: memoryEvent.Query,
            Command: memoryEvent.Command,
            TimestampUtc: memoryEvent.TimestampUtc,
            WasAccepted: memoryEvent.WasAccepted,
            WasSuccessful: memoryEvent.WasSuccessful,
            ConfidenceScore: memoryEvent.ConfidenceScore,
            ContentHash: memoryEvent.ContentHash,
            MachineId: memoryEvent.MachineId);
    }

    private string Redact(string value)
    {
        if (!_configuration.EnableRedaction || string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var result = value;
        result = Regex.Replace(result, @"(?i)(api[_-]?key|token|password)\s*[:=]\s*([^\s""']+)", "$1=<redacted>");
        return result;
    }

    private static string NormalizeTool(string requestedTool, string command)
    {
        if (!string.IsNullOrWhiteSpace(requestedTool) &&
            !requestedTool.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return requestedTool.ToLowerInvariant();
        }

        var firstToken = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstToken) ? "unknown" : firstToken.ToLowerInvariant();
    }

    private static string BuildContentHash(string tool, string query, string command)
    {
        var normalized = $"{tool.Trim().ToLowerInvariant()}|{query.Trim().ToLowerInvariant()}|{command.Trim().ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static double CalculateConfidence(bool wasAccepted, bool wasSuccessful)
    {
        if (wasAccepted && wasSuccessful)
        {
            return 1.0;
        }

        if (wasAccepted)
        {
            return 0.7;
        }

        return 0.3;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return Regex.Matches(value.ToLowerInvariant(), "[a-z0-9_./-]+")
            .Select(m => m.Value)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void TryMigrateLegacyLearningFile()
    {
        var marker = Path.Combine(_storePath, ".migration_v1_done");
        if (File.Exists(marker))
        {
            return;
        }

        var candidatePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "cmdai_learning.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "cmdai_learning.json")
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var candidate in candidatePaths)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(candidate);
                var entries = JsonSerializer.Deserialize<List<LearningEntry>>(json) ?? [];
                foreach (var entry in entries)
                {
                    var normalizedTool = NormalizeTool(entry.Tool, entry.Command);
                    var hash = BuildContentHash(normalizedTool, entry.Query, entry.Command);
                    var memoryEvent = new MemoryEvent(
                        EventId: Guid.NewGuid().ToString("N"),
                        MachineId: _machineId,
                        TimestampUtc: entry.Timestamp,
                        Tool: normalizedTool,
                        Query: entry.Query,
                        Command: entry.Command,
                        WasAccepted: entry.WasAccepted,
                        WasSuccessful: entry.WasSuccessful,
                        ContentHash: hash,
                        ConfidenceScore: entry.ConfidenceScore);
                    AppendEvent(memoryEvent);
                }

                var backupPath = candidate + $".backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Copy(candidate, backupPath, overwrite: false);
            }
            catch
            {
                continue;
            }
        }

        File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
    }
}
