namespace CmdAi.Core.Interfaces;

public interface ICommandFallbackService
{
    string? GetFallbackCommand(string primaryCommand);
}
