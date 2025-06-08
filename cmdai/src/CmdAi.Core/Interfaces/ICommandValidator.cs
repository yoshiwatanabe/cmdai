using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface ICommandValidator
{
    Task<CommandValidationResult> ValidateCommandAsync(string command, string tool);
    bool IsSafeCommand(string command);
    IEnumerable<string> GetDangerousPatterns();
}

public record CommandValidationResult(
    bool IsValid,
    bool IsSafe,
    string? ValidationMessage = null,
    IEnumerable<string>? Warnings = null);