using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class MemoryQueryGenerator : IMemoryQueryGenerator, IMemoryQueryDiagnostics
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500)];
    private const int MaxAttemptsPerProvider = 3;

    private readonly IEnumerable<IAIProvider> _providers;
    private readonly AIConfiguration _configuration;
    private readonly object _traceLock = new();
    private List<ProviderAttemptDiagnostics> _lastTrace = [];

    public MemoryQueryGenerator(IEnumerable<IAIProvider> providers, AIConfiguration configuration)
    {
        _providers = providers;
        _configuration = configuration;
    }

    public async Task<string> GenerateShortQueryAsync(string tool, string command)
    {
        var attempts = new List<ProviderAttemptDiagnostics>();
        var orderedProviders = GetOrderedProviders();
        foreach (var provider in orderedProviders)
        {
            if (!await provider.IsAvailableAsync())
            {
                attempts.Add(new ProviderAttemptDiagnostics(
                    provider.ProviderId,
                    provider.ModelName,
                    false,
                    false,
                    ProviderFailureType.Configuration,
                    "Provider unavailable or missing configuration"));
                continue;
            }

            for (var attempt = 1; attempt <= MaxAttemptsPerProvider; attempt++)
            {
                var prompt = BuildPrompt(tool, command);
                try
                {
                    var response = await provider.GenerateCommandAsync("__memory_query__", prompt);
                    var query = NormalizeGeneratedQuery(response);
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var successMessage = attempt > 1
                            ? $"Succeeded on attempt {attempt}/{MaxAttemptsPerProvider}"
                            : null;
                        attempts.Add(new ProviderAttemptDiagnostics(
                            provider.ProviderId,
                            provider.ModelName,
                            true,
                            true,
                            Message: successMessage));
                        UpdateTrace(attempts);
                        return query;
                    }

                    attempts.Add(new ProviderAttemptDiagnostics(
                        provider.ProviderId,
                        provider.ModelName,
                        true,
                        false,
                        ProviderFailureType.Unknown,
                        $"Empty or invalid query response (attempt {attempt}/{MaxAttemptsPerProvider})"));

                    if (attempt < MaxAttemptsPerProvider)
                    {
                        await Task.Delay(RetryDelays[attempt - 1]);
                        continue;
                    }

                    break;
                }
                catch (AIProviderException ex)
                {
                    var isRetryable = ex.IsTransient && attempt < MaxAttemptsPerProvider;
                    var message = isRetryable
                        ? $"{ex.Message} (attempt {attempt}/{MaxAttemptsPerProvider}, retrying)"
                        : $"{ex.Message} (attempt {attempt}/{MaxAttemptsPerProvider})";
                    attempts.Add(new ProviderAttemptDiagnostics(
                        provider.ProviderId,
                        provider.ModelName,
                        true,
                        false,
                        ex.FailureType,
                        message));

                    if (isRetryable)
                    {
                        await Task.Delay(RetryDelays[attempt - 1]);
                        continue;
                    }

                    break;
                }
                catch (Exception ex)
                {
                    attempts.Add(new ProviderAttemptDiagnostics(
                        provider.ProviderId,
                        provider.ModelName,
                        true,
                        false,
                        ProviderFailureType.Unknown,
                        $"{ex.Message} (attempt {attempt}/{MaxAttemptsPerProvider})"));
                    break;
                }
            }
        }

        UpdateTrace(attempts);
        throw new MemoryQueryGenerationException(
            "Unable to generate a short query from configured AI providers.",
            attempts);
    }

    public IReadOnlyList<ProviderAttemptDiagnostics> GetLastMemoryQueryTrace()
    {
        lock (_traceLock)
        {
            return _lastTrace.ToList();
        }
    }

    private IEnumerable<IAIProvider> GetOrderedProviders()
    {
        var configured = _configuration.GetProviders();
        var ordered = new List<IAIProvider>();

        foreach (var providerName in configured)
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderId.Equals(providerName, StringComparison.OrdinalIgnoreCase));
            if (provider != null)
            {
                ordered.Add(provider);
            }
        }

        foreach (var provider in _providers)
        {
            if (!ordered.Contains(provider))
            {
                ordered.Add(provider);
            }
        }

        return ordered;
    }

    private static string BuildPrompt(string tool, string command)
    {
        return $"Tool: {tool}. Command: {command}. Generate one short natural-language user query (5-12 words), no punctuation at ends.";
    }

    private static string NormalizeGeneratedQuery(string generated)
    {
        var firstLine = generated
            .Trim()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? string.Empty;

        firstLine = firstLine.Trim('"', '\'');
        if (firstLine.StartsWith("- "))
        {
            firstLine = firstLine[2..].Trim();
        }

        if (firstLine.Length > 120)
        {
            firstLine = firstLine[..120].Trim();
        }

        return firstLine;
    }

    private void UpdateTrace(List<ProviderAttemptDiagnostics> attempts)
    {
        lock (_traceLock)
        {
            _lastTrace = attempts;
        }
    }
}
