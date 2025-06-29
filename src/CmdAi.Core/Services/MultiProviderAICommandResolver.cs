using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class MultiProviderAICommandResolver : ICommandResolver
{
    private readonly IEnumerable<IAIProvider> _aiProviders;
    private readonly ICommandValidator _validator;
    private readonly ILearningService _learningService;
    private readonly PatternCommandResolver _fallbackResolver;
    private readonly AIConfiguration _config;

    public MultiProviderAICommandResolver(
        IEnumerable<IAIProvider> aiProviders,
        ICommandValidator validator,
        ILearningService learningService,
        PatternCommandResolver fallbackResolver,
        AIConfiguration config)
    {
        _aiProviders = aiProviders;
        _validator = validator;
        _learningService = learningService;
        _fallbackResolver = fallbackResolver;
        _config = config;
    }

    public bool CanResolve(string tool)
    {
        return true;
    }

    public async Task<CommandResult?> ResolveCommandAsync(CommandRequest request, CommandContext context)
    {
        CommandResult? result = null;

        // Try AI resolution with provider priority if enabled
        if (_config.EnableAI)
        {
            result = await TryAIResolutionWithPriorityAsync(request, context);
        }

        // Fall back to pattern matching if AI fails or is disabled
        if (result == null && _config.FallbackToPatterns && _fallbackResolver.CanResolve(request.Tool))
        {
            result = await _fallbackResolver.ResolveCommandAsync(request, context);
            if (result != null)
            {
                result = result with { Context = $"{result.Context} (AI unavailable, using patterns)" };
            }
        }

        return result;
    }

    private async Task<CommandResult?> TryAIResolutionWithPriorityAsync(CommandRequest request, CommandContext context)
    {
        var providers = GetOrderedProviders();
        
        foreach (var provider in providers)
        {
            try
            {
                var result = await TryProviderAsync(provider, request, context);
                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception)
            {
                // Continue to next provider on error
                continue;
            }
        }

        return null;
    }

    private async Task<CommandResult?> TryProviderAsync(IAIProvider provider, CommandRequest request, CommandContext context)
    {
        try
        {
            // Check if provider is available
            var isAvailable = await provider.IsAvailableAsync();
            if (!isAvailable)
            {
                return null;
            }

            // Get relevant examples from learning service
            var relevantExamples = await _learningService.GetRelevantExamplesAsync(request.Tool, request.Query);
            
            // Build context for AI
            var aiContext = BuildAIContext(request, context, relevantExamples);
            
            // Generate command using AI
            var command = await provider.GenerateCommandAsync(request.Tool, request.Query, aiContext);
            
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            // Validate the generated command
            var validation = await _validator.ValidateCommandAsync(command, request.Tool);
            
            if (!validation.IsValid)
            {
                return null;
            }

            // Create result with AI-specific context
            var description = $"AI-generated {request.Tool} command";
            var resultContext = $"Generated by {provider.ModelName}";
            
            if (!validation.IsSafe)
            {
                resultContext += " ⚠️ POTENTIALLY UNSAFE";
                description += " (requires careful review)";
            }

            if (validation.Warnings?.Any() == true)
            {
                resultContext += $" | Warnings: {string.Join(", ", validation.Warnings)}";
            }

            return new CommandResult(command, description, true, resultContext);
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private IEnumerable<IAIProvider> GetOrderedProviders()
    {
        var configuredProviders = _config.GetProviders();
        var orderedProviders = new List<IAIProvider>();

        // Order providers based on configuration priority
        foreach (var providerName in configuredProviders)
        {
            var provider = _aiProviders.FirstOrDefault(p => 
                p.GetType().Name.ToLowerInvariant().Contains(providerName.ToLowerInvariant()));
            
            if (provider != null)
            {
                orderedProviders.Add(provider);
            }
        }

        // Add any remaining providers not explicitly configured
        var remainingProviders = _aiProviders.Where(p => !orderedProviders.Contains(p));
        orderedProviders.AddRange(remainingProviders);

        return orderedProviders;
    }

    private string BuildAIContext(CommandRequest request, CommandContext context, IEnumerable<LearningEntry> relevantExamples)
    {
        var contextParts = new List<string>();

        // Add working directory context
        if (!string.IsNullOrEmpty(context.WorkingDirectory))
        {
            contextParts.Add($"Working directory: {context.WorkingDirectory}");
        }

        // Add git repository context
        if (context.IsGitRepository)
        {
            contextParts.Add("In a Git repository");
        }

        // Add relevant examples from learning
        if (relevantExamples.Any())
        {
            contextParts.Add("Similar successful commands:");
            foreach (var example in relevantExamples.Take(3))
            {
                contextParts.Add($"'{example.Query}' → {example.Command}");
            }
        }

        return string.Join(". ", contextParts);
    }
}