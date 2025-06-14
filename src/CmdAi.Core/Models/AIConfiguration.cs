using CmdAi.Core.Interfaces;

namespace CmdAi.Core.Models;

public record AIConfiguration(
    bool EnableAI = true,
    string[]? Providers = null,
    string Provider = "ollama",
    string ModelName = "codellama:7b",
    string OllamaEndpoint = "http://localhost:11434",
    string? AzureOpenAIEndpoint = null,
    string? AzureOpenAIApiKey = null,
    string? AzureOpenAIModelName = "model-router",
    int TimeoutSeconds = 30,
    bool FallbackToPatterns = true,
    bool EnableLearning = true,
    double ConfidenceThreshold = 0.7)
{
    public string[] GetProviders() => Providers ?? new[] { Provider };
};

public record AIPromptContext(
    string Tool,
    string Query,
    string? WorkingDirectory = null,
    bool IsGitRepository = false,
    IEnumerable<string>? RecentCommands = null,
    IEnumerable<LearningEntry>? RelevantExamples = null);