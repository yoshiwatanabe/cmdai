using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using CmdAi.Core.Services;
using Xunit;

namespace CmdAi.Tests;

public class MultiProviderAICommandResolverTests
{
    [Fact]
    public async Task ResolveCommandAsync_FailsOverOnTransientFailure()
    {
        var primary = new FakeProvider("openai", "gpt", _ => throw new AIProviderException("openai", ProviderFailureType.RateLimit, "rate limited"));
        var secondary = new FakeProvider("azureopenai", "model-router", _ => Task.FromResult("git status"));
        var resolver = CreateResolver(
            [primary, secondary],
            new AIConfiguration
            {
                Providers = ["openai", "azureopenai"],
                OpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "gpt", ApiKeys = ["k"] },
                AzureOpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "m", ApiKeys = ["k"] }
            });

        var result = await resolver.ResolveCommandAsync(new CommandRequest("git", "show status"), new CommandContext(Environment.CurrentDirectory, true));

        Assert.NotNull(result);
        Assert.Equal(1, primary.GenerateCalls);
        Assert.Equal(1, secondary.GenerateCalls);
    }

    [Fact]
    public async Task ResolveCommandAsync_DoesNotFailoverOnPermanentFailure()
    {
        var primary = new FakeProvider("openai", "gpt", _ => throw new AIProviderException("openai", ProviderFailureType.InvalidRequest, "bad request"));
        var secondary = new FakeProvider("azureopenai", "model-router", _ => Task.FromResult("git branch"));
        var resolver = CreateResolver(
            [primary, secondary],
            new AIConfiguration
            {
                Providers = ["openai", "azureopenai"],
                OpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "gpt", ApiKeys = ["k"] },
                AzureOpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "m", ApiKeys = ["k"] },
                FallbackToPatterns = true
            });

        var result = await resolver.ResolveCommandAsync(new CommandRequest("git", "status"), new CommandContext(Environment.CurrentDirectory, true));

        Assert.NotNull(result);
        Assert.Equal("git status", result!.Command);
        Assert.Equal(1, primary.GenerateCalls);
        Assert.Equal(0, secondary.GenerateCalls);
    }

    [Fact]
    public async Task ResolveCommandAsync_UsesConfiguredOrder()
    {
        var first = new FakeProvider("openai", "gpt", _ => Task.FromResult("git status"));
        var second = new FakeProvider("azureopenai", "model-router", _ => Task.FromResult("git branch"));
        var resolver = CreateResolver(
            [first, second],
            new AIConfiguration
            {
                Providers = ["azureopenai", "openai"],
                OpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "gpt", ApiKeys = ["k"] },
                AzureOpenAI = new ProviderConfiguration { Enabled = true, Endpoint = "x", Model = "m", ApiKeys = ["k"] }
            });

        var result = await resolver.ResolveCommandAsync(new CommandRequest("git", "status"), new CommandContext(Environment.CurrentDirectory, true));

        Assert.NotNull(result);
        Assert.Equal("git branch", result!.Command);
        Assert.Equal(0, first.GenerateCalls);
        Assert.Equal(1, second.GenerateCalls);
    }

    private static MultiProviderAICommandResolver CreateResolver(IEnumerable<IAIProvider> providers, AIConfiguration configuration)
    {
        var validator = new CommandValidator();
        var learningService = new FakeLearningService();
        var fallbackResolver = new PatternCommandResolver(new GitCommandResolver(), new AzureCommandResolver());
        return new MultiProviderAICommandResolver(providers, validator, learningService, fallbackResolver, configuration);
    }

    private sealed class FakeLearningService : ILearningService
    {
        public Task RecordFeedbackAsync(CommandRequest request, CommandResult result, bool wasAccepted, bool wasSuccessful) => Task.CompletedTask;

        public Task<IEnumerable<LearningEntry>> GetRelevantExamplesAsync(string tool, string query)
            => Task.FromResult(Enumerable.Empty<LearningEntry>());

        public Task OptimizeAsync() => Task.CompletedTask;
    }

    private sealed class FakeProvider : IAIProvider
    {
        private readonly Func<(string tool, string query, string? context), Task<string>> _generator;

        public FakeProvider(string providerId, string modelName, Func<(string tool, string query, string? context), Task<string>> generator)
        {
            ProviderId = providerId;
            ModelName = modelName;
            _generator = generator;
        }

        public int GenerateCalls { get; private set; }
        public string ProviderId { get; }
        public string ModelName { get; }

        public Task<bool> IsAvailableAsync() => Task.FromResult(true);

        public Task<string> GenerateCommandAsync(string tool, string naturalLanguageQuery, string? context = null)
        {
            GenerateCalls++;
            return _generator((tool, naturalLanguageQuery, context));
        }
    }
}
