using CmdAi.Core.Models;
using Xunit;

namespace CmdAi.Tests;

public class AIConfigurationTests
{
    [Fact]
    public void GetProviders_RemovesOllama_AndKeepsKnownProviders()
    {
        var config = new AIConfiguration
        {
            Providers = ["openai", "ollama", "azureopenai", "unknown"]
        };

        var providers = config.GetProviders();

        Assert.True(providers.SequenceEqual(new[] { "openai", "azureopenai" }));
    }

    [Fact]
    public void GetConfigurationWarnings_ReportsLegacyOllama()
    {
        var config = new AIConfiguration
        {
            Providers = ["openai", "ollama"]
        };

        var warnings = config.GetConfigurationWarnings().ToList();

        Assert.Contains(warnings, w => w.Contains("ollama", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyLegacyCompatibility_MapsLegacyAzureFields()
    {
        var config = new AIConfiguration
        {
            AzureOpenAIEndpoint = "https://example.azure.com",
            AzureOpenAIApiKey = "legacy-key",
            AzureOpenAIModelName = "legacy-model",
            AzureOpenAI = new ProviderConfiguration
            {
                Endpoint = "",
                Model = "",
                ApiKeys = []
            }
        };

        config.ApplyLegacyCompatibility();

        Assert.Equal("https://example.azure.com", config.AzureOpenAI.Endpoint);
        Assert.Equal("legacy-model", config.AzureOpenAI.Model);
        Assert.True(config.AzureOpenAI.ApiKeys.SequenceEqual(new[] { "legacy-key" }));
    }
}
