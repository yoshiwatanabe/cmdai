using CmdAi.Core.Interfaces;

namespace CmdAi.Core.Models;

public class AIConfiguration
{
    private static readonly string[] DefaultProviderOrder = ["openai", "azureopenai", "anthropic", "gemini"];
    private static readonly HashSet<string> SupportedProviders = new(DefaultProviderOrder, StringComparer.OrdinalIgnoreCase);

    public bool EnableAI { get; set; } = true;
    public string[]? Providers { get; set; }
    public string Provider { get; set; } = "openai"; // Legacy single-provider key
    public int TimeoutSeconds { get; set; } = 30;
    public bool FallbackToPatterns { get; set; } = true;
    public bool EnableLearning { get; set; } = true;
    public double ConfidenceThreshold { get; set; } = 0.7;

    // Legacy fields kept for compatibility/migration.
    public string? AzureOpenAIEndpoint { get; set; }
    public string? AzureOpenAIApiKey { get; set; }
    public string? AzureOpenAIModelName { get; set; }
    public string? OpenAIApiKey { get; set; }
    public string? AnthropicApiKey { get; set; }
    public string? GeminiApiKey { get; set; }
    public string? ModelName { get; set; } // Former Ollama model name
    public string? OllamaEndpoint { get; set; } // Former Ollama endpoint

    public ProviderConfiguration OpenAI { get; set; } = new()
    {
        Enabled = true,
        Endpoint = "https://api.openai.com/v1/chat/completions",
        Model = "gpt-4.1-mini"
    };

    public ProviderConfiguration AzureOpenAI { get; set; } = new()
    {
        Enabled = true,
        Endpoint = "",
        Model = "model-router"
    };

    public ProviderConfiguration Anthropic { get; set; } = new()
    {
        Enabled = true,
        Endpoint = "https://api.anthropic.com/v1/messages",
        Model = "claude-3-5-haiku-latest"
    };

    public ProviderConfiguration Gemini { get; set; } = new()
    {
        Enabled = true,
        Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
        Model = "gemini-2.0-flash"
    };

    public string[] GetProviders()
    {
        var providerNames = (Providers != null && Providers.Length > 0)
            ? Providers
            : (!string.IsNullOrWhiteSpace(Provider) ? [Provider] : DefaultProviderOrder);

        var result = new List<string>();
        foreach (var rawProviderName in providerNames)
        {
            var providerName = (rawProviderName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(providerName))
            {
                continue;
            }

            if (providerName == "ollama")
            {
                continue;
            }

            if (SupportedProviders.Contains(providerName) && !result.Contains(providerName))
            {
                result.Add(providerName);
            }
        }

        return result.Count > 0 ? result.ToArray() : DefaultProviderOrder;
    }

    public IEnumerable<string> GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        var configuredNames = (Providers != null && Providers.Length > 0)
            ? Providers
            : (!string.IsNullOrWhiteSpace(Provider) ? [Provider] : []);

        foreach (var rawProviderName in configuredNames)
        {
            var providerName = (rawProviderName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(providerName))
            {
                continue;
            }

            if (providerName == "ollama")
            {
                warnings.Add("Provider 'ollama' is no longer supported and was ignored.");
                continue;
            }

            if (!SupportedProviders.Contains(providerName))
            {
                warnings.Add($"Unknown provider '{providerName}' was ignored.");
            }
        }

        return warnings;
    }

    public ProviderConfiguration? GetProviderConfiguration(string providerId)
    {
        return providerId.ToLowerInvariant() switch
        {
            "openai" => OpenAI,
            "azureopenai" => AzureOpenAI,
            "anthropic" => Anthropic,
            "gemini" => Gemini,
            _ => null
        };
    }

    public void ApplyLegacyCompatibility()
    {
        if ((OpenAI.ApiKeys == null || OpenAI.ApiKeys.Length == 0))
        {
            var key = OpenAIApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(key))
            {
                OpenAI.ApiKeys = [key];
            }
        }

        // Migrate legacy Azure settings into provider settings when provider-specific fields are missing.
        if (AzureOpenAI is not null)
        {
            if (string.IsNullOrWhiteSpace(AzureOpenAI.Endpoint) && !string.IsNullOrWhiteSpace(AzureOpenAIEndpoint))
            {
                AzureOpenAI.Endpoint = AzureOpenAIEndpoint;
            }

            if ((AzureOpenAI.ApiKeys == null || AzureOpenAI.ApiKeys.Length == 0) && !string.IsNullOrWhiteSpace(AzureOpenAIApiKey))
            {
                AzureOpenAI.ApiKeys = [AzureOpenAIApiKey];
            }

            if (string.IsNullOrWhiteSpace(AzureOpenAI.Model) && !string.IsNullOrWhiteSpace(AzureOpenAIModelName))
            {
                AzureOpenAI.Model = AzureOpenAIModelName;
            }
        }

        if ((Anthropic.ApiKeys == null || Anthropic.ApiKeys.Length == 0))
        {
            var key = AnthropicApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(key))
            {
                Anthropic.ApiKeys = [key];
            }
        }

        if ((Gemini.ApiKeys == null || Gemini.ApiKeys.Length == 0))
        {
            var key = GeminiApiKey ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (!string.IsNullOrWhiteSpace(key))
            {
                Gemini.ApiKeys = [key];
            }
        }
    }
}

public class ProviderConfiguration
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string[] ApiKeys { get; set; } = [];
    public int? TimeoutSeconds { get; set; }
}

public record AIPromptContext(
    string Tool,
    string Query,
    string? WorkingDirectory = null,
    bool IsGitRepository = false,
    IEnumerable<string>? RecentCommands = null,
    IEnumerable<LearningEntry>? RelevantExamples = null);
