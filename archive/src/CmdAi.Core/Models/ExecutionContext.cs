namespace CmdAi.Core.Models;

public record CommandContext(
    string WorkingDirectory,
    bool IsGitRepository = false,
    IDictionary<string, string>? Environment = null);