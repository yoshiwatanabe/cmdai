using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface ICommandResolver
{
    Task<CommandResult?> ResolveCommandAsync(CommandRequest request, CommandContext context);
    bool CanResolve(string tool);
}