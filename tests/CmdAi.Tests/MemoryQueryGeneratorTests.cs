using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using CmdAi.Core.Services;
using Xunit;

namespace CmdAi.Tests;

public class MemoryQueryGeneratorTests
{
    [Fact]
    public async Task GenerateShortQueryAsync_ReturnsQueryFromFirstSuccessfulProvider()
    {
        var primary = new FakeProvider("openai", "gpt", _ => Task.FromResult("show python source path"));
        var generator = CreateGenerator([primary]);

        var result = await generator.GenerateShortQueryAsync("ps", "Get-Command python | % Source");

        Assert.Equal("show python source path", result);
        Assert.Equal(1, primary.GenerateCalls);
    }

    [Fact]
    public async Task GenerateShortQueryAsync_SkipsUnavailableProvider()
    {
        var unavailable = new FakeProvider("openai", "gpt", _ => Task.FromResult("unused"), isAvailable: false);
        var fallback = new FakeProvider("azureopenai", "router", _ => Task.FromResult("get python command source"));
        var generator = CreateGenerator([unavailable, fallback]);

        var result = await generator.GenerateShortQueryAsync("ps", "Get-Command python | % Source");

        Assert.Equal("get python command source", result);
        Assert.Equal(0, unavailable.GenerateCalls);
        Assert.Equal(1, fallback.GenerateCalls);
        var trace = generator.GetLastMemoryQueryTrace();
        Assert.Contains(trace, t => t.ProviderId == "openai" && t.FailureType == ProviderFailureType.Configuration);
    }

    [Fact]
    public async Task GenerateShortQueryAsync_RetriesTransientFailureAndSucceeds()
    {
        var provider = new FakeProvider(
            "openai",
            "gpt",
            callCount =>
            {
                if (callCount == 1)
                {
                    throw new AIProviderException("openai", ProviderFailureType.Timeout, "timed out");
                }

                return Task.FromResult("find python executable path");
            });
        var generator = CreateGenerator([provider]);

        var result = await generator.GenerateShortQueryAsync("ps", "Get-Command python | % Source");

        Assert.Equal("find python executable path", result);
        Assert.Equal(2, provider.GenerateCalls);
        var trace = generator.GetLastMemoryQueryTrace();
        Assert.Contains(trace, t => t.ProviderId == "openai" && t.FailureType == ProviderFailureType.Timeout);
        Assert.Contains(trace, t => t.ProviderId == "openai" && t.Succeeded);
    }

    [Fact]
    public async Task GenerateShortQueryAsync_FailsOverAfterRetryBudgetIsExhausted()
    {
        var primary = new FakeProvider(
            "openai",
            "gpt",
            _ => throw new AIProviderException("openai", ProviderFailureType.Network, "network issue"));
        var secondary = new FakeProvider("azureopenai", "router", _ => Task.FromResult("show python install path"));
        var generator = CreateGenerator([primary, secondary]);

        var result = await generator.GenerateShortQueryAsync("ps", "Get-Command python | % Source");

        Assert.Equal("show python install path", result);
        Assert.Equal(3, primary.GenerateCalls);
        Assert.Equal(1, secondary.GenerateCalls);
    }

    [Fact]
    public async Task GenerateShortQueryAsync_ThrowsDetailedExceptionWhenAllProvidersFail()
    {
        var openai = new FakeProvider(
            "openai",
            "gpt",
            _ => throw new AIProviderException("openai", ProviderFailureType.InvalidRequest, "bad prompt"));
        var azure = new FakeProvider(
            "azureopenai",
            "router",
            _ => throw new AIProviderException("azureopenai", ProviderFailureType.RateLimit, "too many requests"));
        var generator = CreateGenerator([openai, azure]);

        var ex = await Assert.ThrowsAsync<MemoryQueryGenerationException>(
            () => generator.GenerateShortQueryAsync("ps", "Get-Command python | % Source"));

        Assert.Equal("Unable to generate a short query from configured AI providers.", ex.Message);
        Assert.NotEmpty(ex.Attempts);
        Assert.Contains(ex.Attempts, t => t.ProviderId == "openai" && t.FailureType == ProviderFailureType.InvalidRequest);
        Assert.Contains(ex.Attempts, t => t.ProviderId == "azureopenai" && t.FailureType == ProviderFailureType.RateLimit);
        Assert.Equal(ex.Attempts.Count, generator.GetLastMemoryQueryTrace().Count);
    }

    [Fact]
    public async Task GenerateShortQueryAsync_TreatsEmptyResponseAsFailureAndContinues()
    {
        var emptyProvider = new FakeProvider("openai", "gpt", _ => Task.FromResult("   "));
        var fallback = new FakeProvider("azureopenai", "router", _ => Task.FromResult("locate python source file path"));
        var generator = CreateGenerator([emptyProvider, fallback]);

        var result = await generator.GenerateShortQueryAsync("ps", "Get-Command python | % Source");

        Assert.Equal("locate python source file path", result);
        var trace = generator.GetLastMemoryQueryTrace();
        Assert.Contains(trace, t => t.ProviderId == "openai" && t.FailureType == ProviderFailureType.Unknown);
        Assert.Contains(trace, t => t.ProviderId == "azureopenai" && t.Succeeded);
    }

    [Fact]
    public async Task GenerateShortQueryAsync_RetriesEmptyResponseThenSucceedsOnSameProvider()
    {
        var provider = new FakeProvider(
            "openai",
            "gpt",
            callCount => Task.FromResult(callCount < 3 ? " " : "show python command source path"));
        var generator = CreateGenerator([provider]);

        var result = await generator.GenerateShortQueryAsync("ps", "Get-Command python | % Source");

        Assert.Equal("show python command source path", result);
        Assert.Equal(3, provider.GenerateCalls);
        var trace = generator.GetLastMemoryQueryTrace();
        Assert.Contains(trace, t => t.ProviderId == "openai" && t.FailureType == ProviderFailureType.Unknown);
        Assert.Contains(trace, t => t.ProviderId == "openai" && t.Succeeded);
    }

    private static MemoryQueryGenerator CreateGenerator(IEnumerable<IAIProvider> providers)
    {
        return new MemoryQueryGenerator(
            providers,
            new AIConfiguration
            {
                Providers = ["openai", "azureopenai"],
                OpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "gpt", ApiKeys = ["k"] },
                AzureOpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "router", ApiKeys = ["k"] }
            });
    }

    private sealed class FakeProvider : IAIProvider
    {
        private readonly Func<int, Task<string>> _generate;
        private readonly bool _isAvailable;

        public FakeProvider(string providerId, string modelName, Func<int, Task<string>> generate, bool isAvailable = true)
        {
            ProviderId = providerId;
            ModelName = modelName;
            _generate = generate;
            _isAvailable = isAvailable;
        }

        public int GenerateCalls { get; private set; }
        public string ProviderId { get; }
        public string ModelName { get; }

        public Task<bool> IsAvailableAsync() => Task.FromResult(_isAvailable);

        public Task<string> GenerateCommandAsync(string tool, string naturalLanguageQuery, string? context = null)
        {
            GenerateCalls++;
            return _generate(GenerateCalls);
        }
    }
}
