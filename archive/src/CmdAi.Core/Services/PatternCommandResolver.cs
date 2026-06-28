using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class PatternCommandResolver : ICommandResolver
{
    private readonly GitCommandResolver _gitResolver;
    private readonly AzureCommandResolver _azureResolver;

    public PatternCommandResolver(GitCommandResolver gitResolver, AzureCommandResolver azureResolver)
    {
        _gitResolver = gitResolver;
        _azureResolver = azureResolver;
    }

    public bool CanResolve(string tool)
    {
        return _gitResolver.CanResolve(tool) || _azureResolver.CanResolve(tool);
    }

    public async Task<CommandResult?> ResolveCommandAsync(CommandRequest request, CommandContext context)
    {
        // Try git resolver first
        if (_gitResolver.CanResolve(request.Tool))
        {
            var gitResult = await _gitResolver.ResolveCommandAsync(request, context);
            if (gitResult != null)
            {
                // Mark as pattern-based for learning purposes
                return gitResult with { Context = $"{gitResult.Context} (Pattern-based)" };
            }
        }

        // Try Azure resolver
        if (_azureResolver.CanResolve(request.Tool))
        {
            var azureResult = await _azureResolver.ResolveCommandAsync(request, context);
            if (azureResult != null)
            {
                // Mark as pattern-based for learning purposes
                return azureResult with { Context = $"{azureResult.Context} (Pattern-based)" };
            }
        }

        return null;
    }
}