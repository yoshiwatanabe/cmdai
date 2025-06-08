using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class InMemoryCommandRepository : ICommandRepository
{
    private readonly List<CommandEntry> _commands = new();

    public Task<bool> AddCommandAsync(string tool, string pattern, string command, string description)
    {
        _commands.Add(new CommandEntry(tool, pattern, command, description));
        return Task.FromResult(true);
    }

    public Task<IEnumerable<CommandResult>> SearchCommandsAsync(string tool, string query)
    {
        var results = _commands
            .Where(c => c.Tool.Equals(tool, StringComparison.OrdinalIgnoreCase))
            .Where(c => query.Contains(c.Pattern, StringComparison.OrdinalIgnoreCase))
            .Select(c => new CommandResult(c.Command, c.Description));

        return Task.FromResult(results);
    }

    private record CommandEntry(string Tool, string Pattern, string Command, string Description);
}