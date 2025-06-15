using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface ICommandRepository
{
    Task<IEnumerable<CommandResult>> SearchCommandsAsync(string tool, string query);
    Task<bool> AddCommandAsync(string tool, string pattern, string command, string description);
}