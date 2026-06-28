using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface IContextProvider
{
    Task<CommandContext> GetContextAsync(string? workingDirectory = null);
}