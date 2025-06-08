using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class ContextProvider : IContextProvider
{
    public async Task<CommandContext> GetContextAsync(string? workingDirectory = null)
    {
        var currentDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
        var isGitRepository = await IsGitRepositoryAsync(currentDirectory);
        
        var environment = new Dictionary<string, string>();
        foreach (var env in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
        {
            if (env.Key is string key && env.Value is string value)
            {
                environment[key] = value;
            }
        }

        return new CommandContext(currentDirectory, isGitRepository, environment);
    }

    private Task<bool> IsGitRepositoryAsync(string directory)
    {
        try
        {
            var current = new DirectoryInfo(directory);
            
            while (current != null)
            {
                if (current.GetDirectories(".git").Any())
                {
                    return Task.FromResult(true);
                }
                current = current.Parent;
            }
            
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}