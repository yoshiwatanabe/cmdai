using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class MemoryQueryGenerator : IMemoryQueryGenerator
{
    private readonly IEnumerable<IAIProvider> _providers;
    private readonly AIConfiguration _configuration;

    public MemoryQueryGenerator(IEnumerable<IAIProvider> providers, AIConfiguration configuration)
    {
        _providers = providers;
        _configuration = configuration;
    }

    public async Task<string> GenerateShortQueryAsync(string tool, string command)
    {
        var orderedProviders = GetOrderedProviders();
        foreach (var provider in orderedProviders)
        {
            try
            {
                if (!await provider.IsAvailableAsync())
                {
                    continue;
                }

                var prompt = BuildPrompt(tool, command);
                var response = await provider.GenerateCommandAsync("__memory_query__", prompt);
                var query = NormalizeGeneratedQuery(response);
                if (!string.IsNullOrWhiteSpace(query))
                {
                    return query;
                }
            }
            catch
            {
                continue;
            }
        }

        throw new InvalidOperationException("Unable to generate a short query from configured AI providers.");
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
}
