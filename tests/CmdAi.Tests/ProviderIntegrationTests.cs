using System.Net.Http;
using CmdAi.Core.Models;
using CmdAi.Core.Services;
using Xunit;

namespace CmdAi.Tests;

[Collection("ProviderIntegration")]
public sealed class ProviderIntegrationTests
{
    [Fact]
    public async Task OpenAIProvider_RealApi_GeneratesCommand()
    {
        if (!IntegrationEnabled())
        {
            return;
        }

        var config = CreateBaseConfig();
        var apiKey = GetOptionalSetting("AI__OpenAI__ApiKeys__0");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        config.OpenAI = new ProviderConfiguration
        {
            Enabled = true,
            Endpoint = GetValueOrDefault("AI__OpenAI__Endpoint", "https://api.openai.com/v1/chat/completions"),
            Model = GetValueOrDefault("AI__OpenAI__Model", "gpt-5-mini"),
            ApiKeys = [apiKey]
        };

        using var httpClient = new HttpClient();
        var provider = new OpenAIProvider(httpClient, config);

        var command = await provider.GenerateCommandAsync("git", "show git status");
        Assert.False(string.IsNullOrWhiteSpace(command));
    }

    [Fact]
    public async Task AzureOpenAIProvider_RealApi_GeneratesCommand()
    {
        if (!IntegrationEnabled())
        {
            return;
        }

        var endpoint = GetOptionalSetting("AI__AzureOpenAI__Endpoint");
        var apiKey = GetOptionalSetting("AI__AzureOpenAI__ApiKeys__0");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var config = CreateBaseConfig();
        config.AzureOpenAI = new ProviderConfiguration
        {
            Enabled = true,
            Endpoint = endpoint,
            Model = GetValueOrDefault("AI__AzureOpenAI__Model", "DeepSeek-R1-0528"),
            ApiKeys = [apiKey]
        };

        using var httpClient = new HttpClient();
        var provider = new AzureOpenAIProvider(httpClient, config);

        var command = await provider.GenerateCommandAsync("git", "show git status");
        Assert.False(string.IsNullOrWhiteSpace(command));
    }

    [Fact]
    public async Task AnthropicProvider_RealApi_GeneratesCommand()
    {
        if (!IntegrationEnabled())
        {
            return;
        }

        var apiKey = GetOptionalSetting("AI__Anthropic__ApiKeys__0") ?? GetOptionalSetting("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var config = CreateBaseConfig();
        config.Anthropic = new ProviderConfiguration
        {
            Enabled = true,
            Endpoint = GetValueOrDefault("AI__Anthropic__Endpoint", "https://api.anthropic.com/v1/messages"),
            Model = GetValueOrDefault("AI__Anthropic__Model", "claude-3-5-haiku-latest"),
            ApiKeys = [apiKey]
        };

        using var httpClient = new HttpClient();
        var provider = new AnthropicProvider(httpClient, config);

        var command = await provider.GenerateCommandAsync("git", "show git status");
        Assert.False(string.IsNullOrWhiteSpace(command));
    }

    [Fact]
    public async Task GeminiProvider_RealApi_GeneratesCommand()
    {
        if (!IntegrationEnabled())
        {
            return;
        }

        var apiKey = GetOptionalSetting("AI__Gemini__ApiKeys__0") ?? GetOptionalSetting("GOOGLE_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var config = CreateBaseConfig();
        config.Gemini = new ProviderConfiguration
        {
            Enabled = true,
            Endpoint = GetValueOrDefault("AI__Gemini__Endpoint", "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"),
            Model = GetValueOrDefault("AI__Gemini__Model", "gemini-2.0-flash"),
            ApiKeys = [apiKey]
        };

        using var httpClient = new HttpClient();
        var provider = new GeminiProvider(httpClient, config);

        var command = await provider.GenerateCommandAsync("git", "show git status");
        Assert.False(string.IsNullOrWhiteSpace(command));
    }

    private static AIConfiguration CreateBaseConfig()
    {
        return new AIConfiguration
        {
            TimeoutSeconds = 60
        };
    }

    private static bool IntegrationEnabled()
    {
        var flag = Environment.GetEnvironmentVariable("RUN_CMD_AI_INTEGRATION_TESTS");
        return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetOptionalSetting(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return TryReadHomeDotEnvValue(key);
    }

    private static string GetValueOrDefault(string key, string fallback)
    {
        var value = GetOptionalSetting(key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }

    private static string? TryReadHomeDotEnvValue(string key)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return null;
        }

        var path = Path.Combine(home, ".env");
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var candidateKey = trimmed[..separatorIndex].Trim();
            if (!candidateKey.Equals(key, StringComparison.Ordinal))
            {
                continue;
            }

            var value = trimmed[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }
}

[CollectionDefinition("ProviderIntegration", DisableParallelization = true)]
public sealed class ProviderIntegrationCollectionDefinition
{
}
