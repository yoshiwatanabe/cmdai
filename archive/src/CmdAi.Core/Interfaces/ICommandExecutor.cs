using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface ICommandExecutor
{
    Task<bool> ExecuteAsync(CommandResult command, CommandContext context);
    Task<bool> ConfirmExecutionAsync(CommandResult command);
}