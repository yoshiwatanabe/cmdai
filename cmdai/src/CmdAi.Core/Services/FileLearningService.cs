using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using System.Text.Json;

namespace CmdAi.Core.Services;

public class FileLearningService : ILearningService
{
    private readonly string _learningDataPath;
    private readonly List<LearningEntry> _learningData;
    private readonly object _lock = new object();

    public FileLearningService(string learningDataPath = "cmdai_learning.json")
    {
        _learningDataPath = learningDataPath;
        _learningData = LoadLearningData();
    }

    public Task RecordFeedbackAsync(CommandRequest request, CommandResult result, bool wasAccepted, bool wasSuccessful)
    {
        lock (_lock)
        {
            var entry = new LearningEntry(
                Tool: request.Tool,
                Query: request.Query,
                Command: result.Command,
                Timestamp: DateTime.UtcNow,
                WasAccepted: wasAccepted,
                WasSuccessful: wasSuccessful,
                ConfidenceScore: CalculateConfidenceScore(wasAccepted, wasSuccessful)
            );

            _learningData.Add(entry);
            
            // Keep only recent entries (last 1000)
            if (_learningData.Count > 1000)
            {
                _learningData.RemoveRange(0, _learningData.Count - 1000);
            }

            SaveLearningData();
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<LearningEntry>> GetRelevantExamplesAsync(string tool, string query)
    {
        lock (_lock)
        {
            var queryWords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var relevantEntries = _learningData
                .Where(entry => entry.Tool.Equals(tool, StringComparison.OrdinalIgnoreCase))
                .Where(entry => entry.WasAccepted && entry.WasSuccessful) // Only successful examples
                .Where(entry => HasQueryOverlap(entry.Query, queryWords))
                .OrderByDescending(entry => entry.ConfidenceScore)
                .ThenByDescending(entry => entry.Timestamp)
                .Take(5);

            return Task.FromResult(relevantEntries);
        }
    }

    public Task OptimizeAsync()
    {
        lock (_lock)
        {
            // Remove old unsuccessful entries (older than 30 days)
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var toRemove = _learningData
                .Where(entry => entry.Timestamp < cutoffDate && (!entry.WasAccepted || !entry.WasSuccessful))
                .ToList();

            foreach (var entry in toRemove)
            {
                _learningData.Remove(entry);
            }

            // Update confidence scores based on recent patterns
            UpdateConfidenceScores();
            
            SaveLearningData();
        }

        return Task.CompletedTask;
    }

    private List<LearningEntry> LoadLearningData()
    {
        try
        {
            if (!File.Exists(_learningDataPath))
            {
                return new List<LearningEntry>();
            }

            var json = File.ReadAllText(_learningDataPath);
            var data = JsonSerializer.Deserialize<List<LearningEntry>>(json);
            return data ?? new List<LearningEntry>();
        }
        catch
        {
            // If file is corrupted, start fresh
            return new List<LearningEntry>();
        }
    }

    private void SaveLearningData()
    {
        try
        {
            var json = JsonSerializer.Serialize(_learningData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_learningDataPath, json);
        }
        catch
        {
            // Silently fail - learning data is not critical
        }
    }

    private double CalculateConfidenceScore(bool wasAccepted, bool wasSuccessful)
    {
        if (wasAccepted && wasSuccessful)
            return 1.0;
        if (wasAccepted && !wasSuccessful)
            return 0.7;
        if (!wasAccepted)
            return 0.3;
        
        return 0.5;
    }

    private bool HasQueryOverlap(string entryQuery, string[] queryWords)
    {
        var entryWords = entryQuery.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Check for word overlap
        var overlap = queryWords.Intersect(entryWords).Count();
        var totalWords = Math.Max(queryWords.Length, entryWords.Length);
        
        // Require at least 30% overlap
        return (double)overlap / totalWords >= 0.3;
    }

    private void UpdateConfidenceScores()
    {
        // Group by similar queries and boost confidence for frequently successful commands
        var groups = _learningData
            .Where(e => e.WasAccepted && e.WasSuccessful)
            .GroupBy(e => new { e.Tool, e.Command })
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var successRate = (double)group.Count(e => e.WasSuccessful) / group.Count();
            var boost = Math.Min(0.2, successRate * 0.1);
            
            // Update confidence scores for entries in this group
            foreach (var entry in group)
            {
                var index = _learningData.IndexOf(entry);
                if (index >= 0)
                {
                    _learningData[index] = entry with { ConfidenceScore = Math.Min(1.0, entry.ConfidenceScore + boost) };
                }
            }
        }
    }
}