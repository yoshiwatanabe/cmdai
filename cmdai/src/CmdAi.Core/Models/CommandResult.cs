namespace CmdAi.Core.Models;

public record CommandResult(
    string Command,
    string Description,
    bool RequiresConfirmation = true,
    string? Context = null);