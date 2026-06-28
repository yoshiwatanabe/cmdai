using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class CompositeCommandResolver : ICommandResolver
{
    private readonly List<ICommandResolver> _resolvers;

    public CompositeCommandResolver(IEnumerable<ICommandResolver> resolvers)
    {
        _resolvers = resolvers.ToList();
    }

    public bool CanResolve(string tool)
    {
        return _resolvers.Any(resolver => resolver.CanResolve(tool));
    }

    public async Task<CommandResult?> ResolveCommandAsync(CommandRequest request, CommandContext context)
    {
        foreach (var resolver in _resolvers)
        {
            if (resolver.CanResolve(request.Tool))
            {
                var result = await resolver.ResolveCommandAsync(request, context);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }
}