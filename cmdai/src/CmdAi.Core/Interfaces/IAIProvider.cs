namespace CmdAi.Core.Interfaces;

public interface IAIProvider
{
    Task<string> GenerateCommandAsync(string tool, string naturalLanguageQuery, string? context = null);
    Task<bool> IsAvailableAsync();
    string ModelName { get; }
}