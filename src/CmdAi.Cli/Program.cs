using System.CommandLine;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using CmdAi.Core.Services;
using DotNetEnv;

namespace CmdAi.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = ConfigureServices();
        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = new RootCommand("AI-powered CLI assistant that translates natural language to CLI commands");
        var rootQueryArg = new Argument<string?>("query", () => null, "Natural language request (universal mode)");
        var rootQueryOption = new Option<string?>("--query", "Natural language request (option style)");
        rootCommand.AddArgument(rootQueryArg);
        rootCommand.AddOption(rootQueryOption);

        // Add diagnostics command
        var diagnosticsCommand = new Command("diagnostics", "Show configuration and provider status");
        diagnosticsCommand.SetHandler(async () =>
        {
            await ShowDiagnosticsAsync(serviceProvider);
        });
        
        rootCommand.AddCommand(diagnosticsCommand);

        var toolArg = new Argument<string>("tool", "The tool to get help with (e.g., git, az)");
        var queryArg = new Argument<string>("query", "Natural language description of what you want to do");
        
        var askCommand = new Command("ask", "Ask for help with a specific tool");
        askCommand.AddArgument(toolArg);
        askCommand.AddArgument(queryArg);

        askCommand.SetHandler<string, string>(async (tool, query) =>
        {
            var request = new CommandRequest(tool, query, IsDirectCommand: false);
            await HandleCommandAsync(serviceProvider, request);
        }, toolArg, queryArg);

        var directToolCommands = new[] { "git", "az", "docker", "kubectl", "npm", "yarn", "ps" };
        foreach (var toolName in directToolCommands)
        {
            var directQueryArg = new Argument<string>("query", $"Natural language description of the {toolName} operation");
            var toolCommand = new Command(toolName, $"Direct {toolName} command assistance");
            toolCommand.AddArgument(directQueryArg);

            toolCommand.SetHandler<string>(async (query) =>
            {
                var request = new CommandRequest(toolName, query, IsDirectCommand: true);
                await HandleCommandAsync(serviceProvider, request);
            }, directQueryArg);

            rootCommand.AddCommand(toolCommand);
        }

        rootCommand.AddCommand(BuildHiddenAliasCommand("azure", "az", serviceProvider));
        rootCommand.AddCommand(BuildHiddenAliasCommand("pwsh", "ps", serviceProvider));
        rootCommand.AddCommand(BuildHiddenAliasCommand("powershell", "ps", serviceProvider));

        rootCommand.AddCommand(askCommand);
        rootCommand.AddCommand(BuildMemoryCommand(serviceProvider));
        rootCommand.SetHandler<string?, string?>(async (queryArgValue, queryOptionValue) =>
        {
            if (!string.IsNullOrWhiteSpace(queryArgValue) &&
                !string.IsNullOrWhiteSpace(queryOptionValue) &&
                !string.Equals(queryArgValue, queryOptionValue, StringComparison.Ordinal))
            {
                Console.WriteLine("Error: provide either positional <query> or --query, or ensure both values match.");
                return;
            }

            var query = !string.IsNullOrWhiteSpace(queryOptionValue) ? queryOptionValue : queryArgValue;
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("Provide a natural language request or use --help.");
                return;
            }

            var request = new CommandRequest("auto", query, IsDirectCommand: false);
            await HandleCommandAsync(serviceProvider, request);
        }, rootQueryArg, rootQueryOption);

        return await rootCommand.InvokeAsync(args);
    }

    static void ShowVersionInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version?.ToString();
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "CmdAI";
        
        Console.WriteLine($"{title} v{informationalVersion}");
        Console.WriteLine("AI-powered CLI assistant with multi-provider API failover");
        Console.WriteLine();
        Console.WriteLine("Repository: https://github.com/yoshiwatanabe/cmdai");
        Console.WriteLine("License: MIT");
    }

    static async Task ShowDiagnosticsAsync(ServiceProvider serviceProvider)
    {
        var aiConfig = serviceProvider.GetRequiredService<AIConfiguration>();
        var memoryConfig = serviceProvider.GetRequiredService<MemoryConfiguration>();
        var aiProviders = serviceProvider.GetServices<IAIProvider>();

        Console.WriteLine("=== CmdAI Diagnostics ===");
        Console.WriteLine();

        // Show configuration
        Console.WriteLine("📋 Configuration:");
        Console.WriteLine($"  AI Enabled: {aiConfig.EnableAI}");
        Console.WriteLine($"  Configured Providers: [{string.Join(", ", aiConfig.GetProviders())}]");
        Console.WriteLine($"  Fallback to Patterns: {aiConfig.FallbackToPatterns}");
        Console.WriteLine($"  Timeout: {aiConfig.TimeoutSeconds}s");
        foreach (var warning in aiConfig.GetConfigurationWarnings())
        {
            Console.WriteLine($"  Warning: {warning}");
        }
        Console.WriteLine();

        Console.WriteLine("🧠 Memory Configuration:");
        Console.WriteLine($"  StorePath: {memoryConfig.StorePath ?? "(default: ~/.cmdai/memory)"}");
        Console.WriteLine($"  CandidateCap: {memoryConfig.CandidateCap}");
        Console.WriteLine($"  HighConfidenceThreshold: {memoryConfig.HighConfidenceThreshold:0.00}");
        Console.WriteLine($"  Redaction Enabled: {memoryConfig.EnableRedaction}");
        Console.WriteLine();

        // Show environment file detection
        Console.WriteLine("🔍 Environment File Detection:");
        var homeEnvPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".env");
        var currentEnvPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        
        Console.WriteLine($"  Home .env: {homeEnvPath}");
        Console.WriteLine($"    Exists: {File.Exists(homeEnvPath)}");
        if (File.Exists(homeEnvPath))
        {
            var lines = File.ReadAllLines(homeEnvPath).Where(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l));
            Console.WriteLine($"    Configuration lines: {lines.Count()}");
            foreach (var line in lines.Take(5))
            {
                var key = line.Split('=')[0];
                Console.WriteLine($"      {key}=***");
            }
        }
        
        Console.WriteLine($"  Current .env: {currentEnvPath}");
        Console.WriteLine($"    Exists: {File.Exists(currentEnvPath)}");
        Console.WriteLine();

        Console.WriteLine("🌐 Provider Configuration:");
        ShowProviderConfiguration("openai", aiConfig.OpenAI);
        ShowProviderConfiguration("azureopenai", aiConfig.AzureOpenAI);
        ShowProviderConfiguration("anthropic", aiConfig.Anthropic);
        ShowProviderConfiguration("gemini", aiConfig.Gemini);
        Console.WriteLine();

        // Test provider connectivity
        Console.WriteLine("🔌 Provider Connectivity Tests:");
        foreach (var provider in aiProviders)
        {
            Console.Write($"  {provider.ProviderId}: ");
            try
            {
                var isAvailable = await provider.IsAvailableAsync();
                Console.WriteLine(isAvailable ? "✅ Available" : "❌ Unavailable");
                
                if (isAvailable)
                {
                    Console.WriteLine($"    Model: {provider.ModelName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }
        Console.WriteLine();

        // Show provider priority order
        Console.WriteLine("🎯 Provider Priority Order:");
        var orderedProviders = GetOrderedProvidersForDiagnostics(aiProviders, aiConfig).ToList();
        for (int i = 0; i < orderedProviders.Count(); i++)
        {
            var provider = orderedProviders.ElementAt(i);
            Console.WriteLine($"  {i + 1}. {provider.ProviderId} ({provider.ModelName})");
        }
        Console.WriteLine();

        // Test a simple command to see which provider is actually used
        Console.WriteLine("🧪 Provider Selection Test:");
        Console.WriteLine("  Testing which provider would be selected for a git command...");
        try
        {
            var contextProvider = serviceProvider.GetRequiredService<IContextProvider>();
            var commandResolver = serviceProvider.GetRequiredService<ICommandResolver>();
            var context = await contextProvider.GetContextAsync();
            var request = new CommandRequest("git", "show status", IsDirectCommand: true);
            var result = await commandResolver.ResolveCommandAsync(request, context);
            
            if (result != null)
            {
                Console.WriteLine($"  Selected provider: {result.Context}");
                Console.WriteLine($"  Generated command: {result.Command}");
            }
            else
            {
                Console.WriteLine("  No provider could resolve the command");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error during test: {ex.Message}");
        }

        if (serviceProvider.GetService<IResolutionDiagnostics>() is { } diagnostics)
        {
            Console.WriteLine();
            Console.WriteLine("📈 Last Failover Trace:");
            var trace = diagnostics.GetLastResolutionTrace();
            if (trace.Count == 0)
            {
                Console.WriteLine("  No provider attempts were recorded yet.");
            }
            else
            {
                foreach (var attempt in trace)
                {
                    var status = attempt.Succeeded ? "✅ Success" : "❌ Failed";
                    var failureType = attempt.FailureType.HasValue ? $" [{attempt.FailureType.Value}]" : string.Empty;
                    Console.WriteLine($"  {attempt.ProviderId}: {status}{failureType}");
                    if (!string.IsNullOrWhiteSpace(attempt.Message))
                    {
                        Console.WriteLine($"    {attempt.Message}");
                    }
                }
            }
        }

        if (serviceProvider.GetService<IMemoryQueryDiagnostics>() is { } memoryDiagnostics)
        {
            Console.WriteLine();
            Console.WriteLine("🧾 Last Memory Query Trace:");
            var trace = memoryDiagnostics.GetLastMemoryQueryTrace();
            if (trace.Count == 0)
            {
                Console.WriteLine("  No memory query attempts were recorded yet.");
            }
            else
            {
                foreach (var attempt in trace)
                {
                    var status = attempt.Succeeded ? "✅ Success" : "❌ Failed";
                    var failureType = attempt.FailureType.HasValue ? $" [{attempt.FailureType.Value}]" : string.Empty;
                    Console.WriteLine($"  {attempt.ProviderId}: {status}{failureType}");
                    if (!string.IsNullOrWhiteSpace(attempt.Message))
                    {
                        Console.WriteLine($"    {attempt.Message}");
                    }
                }
            }
        }
    }

    static IEnumerable<IAIProvider> GetOrderedProvidersForDiagnostics(IEnumerable<IAIProvider> aiProviders, AIConfiguration config)
    {
        var configuredProviders = config.GetProviders();
        var orderedProviders = new List<IAIProvider>();

        foreach (var providerName in configuredProviders)
        {
            var provider = aiProviders.FirstOrDefault(p =>
                p.ProviderId.Equals(providerName, StringComparison.OrdinalIgnoreCase));
            if (provider != null)
            {
                orderedProviders.Add(provider);
            }
        }

        var remainingProviders = aiProviders.Where(p => !orderedProviders.Contains(p));
        orderedProviders.AddRange(remainingProviders);

        return orderedProviders;
    }

    static void ShowProviderConfiguration(string providerId, ProviderConfiguration configuration)
    {
        Console.WriteLine($"  {providerId}:");
        Console.WriteLine($"    Enabled: {configuration.Enabled}");
        Console.WriteLine($"    Endpoint: {(string.IsNullOrWhiteSpace(configuration.Endpoint) ? "Not configured" : "Configured")}");
        Console.WriteLine($"    Model: {(string.IsNullOrWhiteSpace(configuration.Model) ? "Not configured" : configuration.Model)}");
        Console.WriteLine($"    API Keys: {(configuration.ApiKeys.Length > 0 ? $"{configuration.ApiKeys.Length} configured" : "Not configured")}");
    }

    static async Task HandleCommandAsync(ServiceProvider serviceProvider, CommandRequest request)
    {
        var contextProvider = serviceProvider.GetRequiredService<IContextProvider>();
        var commandResolver = serviceProvider.GetRequiredService<ICommandResolver>();
        var commandExecutor = serviceProvider.GetRequiredService<ICommandExecutor>();
        var learningService = serviceProvider.GetRequiredService<ILearningService>();
        var memoryService = serviceProvider.GetRequiredService<IMemoryService>();
        var toolAvailability = serviceProvider.GetRequiredService<IToolAvailabilityService>();
        var fallbackService = serviceProvider.GetRequiredService<ICommandFallbackService>();

        try
        {
            var effectiveRequest = ApplyToolHint(request);
            var context = await contextProvider.GetContextAsync();
            var recalled = await memoryService.FindBestMatchAsync(effectiveRequest);
            if (recalled?.IsHighConfidence == true)
            {
                var memoryTool = InferToolFromCommand(recalled.Entry.Command);
                Console.WriteLine($"Memory match ({recalled.Score:0.00}): {recalled.Reason}");
                Console.WriteLine($"Suggested command: {recalled.Entry.Command}");
                Console.WriteLine($"Inferred tool: {memoryTool}");
                Console.WriteLine("Reason: learned from previous successful interaction");
                var memoryResult = new CommandResult(
                    recalled.Entry.Command,
                    "Memory-recalled command",
                    true,
                    $"Memory recall ({recalled.Score:0.00})",
                    memoryTool,
                    "Previous accepted/successful interaction");

                var useMemory = await commandExecutor.ConfirmExecutionAsync(memoryResult);
                if (useMemory)
                {
                    var succeeded = await commandExecutor.ExecuteAsync(memoryResult, context);
                    await memoryService.RecordAsync(effectiveRequest, memoryResult, true, succeeded);
                    await learningService.RecordFeedbackAsync(effectiveRequest, memoryResult, true, succeeded);
                    return;
                }

                Console.WriteLine("Memory suggestion rejected. Continuing with AI inference...");
            }

            var result = await commandResolver.ResolveCommandAsync(effectiveRequest, context);

            if (result == null)
            {
                Console.WriteLine($"Sorry, I couldn't find a command for '{effectiveRequest.Query}' with {effectiveRequest.Tool}");
                return;
            }

            var inferredTool = result.InferredTool ?? InferToolFromCommand(result.Command);
            Console.WriteLine($"Suggested command: {result.Command}");
            Console.WriteLine($"Description: {result.Description}");
            Console.WriteLine($"Inferred tool: {inferredTool}");
            Console.WriteLine($"Reason: {result.Reason ?? "AI inference based on your request and context"}");

            var firstToken = GetFirstToken(result.Command);
            if (!string.IsNullOrWhiteSpace(firstToken) && !toolAvailability.IsAvailable(firstToken))
            {
                var fallbackCommand = fallbackService.GetFallbackCommand(result.Command);
                if (!string.IsNullOrWhiteSpace(fallbackCommand))
                {
                    Console.WriteLine($"Fallback command: {fallbackCommand}");
                    Console.WriteLine($"Note: '{firstToken}' is not available on this machine.");
                }
            }

            if (result.Context != null)
            {
                Console.WriteLine($"Context: {result.Context}");
            }

            bool wasAccepted = false;
            bool wasSuccessful = false;

            if (result.RequiresConfirmation)
            {
                var confirmed = await commandExecutor.ConfirmExecutionAsync(result);
                wasAccepted = confirmed;
                
                if (confirmed)
                {
                    wasSuccessful = await commandExecutor.ExecuteAsync(result, context);
                }
                else
                {
                    Console.WriteLine("Command execution cancelled.");
                }
            }
            else
            {
                wasAccepted = true;
                wasSuccessful = await commandExecutor.ExecuteAsync(result, context);
            }

            // Record feedback for learning (only if AI-generated or pattern-based)
            if (result.Context?.Contains("Generated by") == true || result.Context?.Contains("Pattern-based") == true)
            {
                await learningService.RecordFeedbackAsync(effectiveRequest, result, wasAccepted, wasSuccessful);
            }

            await memoryService.RecordAsync(effectiveRequest, result, wasAccepted, wasSuccessful);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static Command BuildMemoryCommand(ServiceProvider serviceProvider)
    {
        var memoryCommand = new Command("memory", "Inspect and manage learned command memory");
        var addCommand = new Command("add", "Add a known command to memory");
        var addCommandArg = new Argument<string>("command", "Known command to store");
        var addQueryOption = new Option<string?>("--query", "Optional query override");
        var addToolOption = new Option<string?>("--tool", "Optional tool override");
        var addForceOption = new Option<bool>("--force", "Skip confirmation prompt");
        addCommand.AddArgument(addCommandArg);
        addCommand.AddOption(addQueryOption);
        addCommand.AddOption(addToolOption);
        addCommand.AddOption(addForceOption);
        addCommand.SetHandler<string, string?, string?, bool>(async (command, queryOverride, toolOverride, force) =>
        {
            var memoryService = serviceProvider.GetRequiredService<IMemoryService>();
            var queryGenerator = serviceProvider.GetRequiredService<IMemoryQueryGenerator>();
            var validator = serviceProvider.GetRequiredService<ICommandValidator>();

            var inferredTool = !string.IsNullOrWhiteSpace(toolOverride)
                ? toolOverride.Trim().ToLowerInvariant()
                : InferToolFromCommand(command);

            var validation = await validator.ValidateCommandAsync(command, inferredTool);
            if (!validation.IsValid)
            {
                Console.WriteLine($"Warning: command may not be a valid {inferredTool} command.");
                Console.WriteLine($"Validator message: {validation.ValidationMessage}");
            }

            string generatedQuery;
            if (!string.IsNullOrWhiteSpace(queryOverride))
            {
                generatedQuery = queryOverride.Trim();
            }
            else
            {
                try
                {
                    generatedQuery = await queryGenerator.GenerateShortQueryAsync(inferredTool, command);
                }
                catch (MemoryQueryGenerationException ex)
                {
                    Console.WriteLine($"Error: unable to generate query for memory add. {ex.Message}");
                    foreach (var attempt in ex.Attempts)
                    {
                        var failureType = attempt.FailureType?.ToString() ?? "Unknown";
                        var message = string.IsNullOrWhiteSpace(attempt.Message) ? "No details available" : attempt.Message;
                        Console.WriteLine($"  {attempt.ProviderId} ({attempt.ModelName}): {failureType} - {message}");
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: unable to generate query for memory add. {ex.Message}");
                    return;
                }
            }

            Console.WriteLine("Memory add preview:");
            Console.WriteLine($"  Command: {command}");
            Console.WriteLine($"  Tool: {inferredTool}");
            Console.WriteLine($"  Query: {generatedQuery}");
            Console.WriteLine("  Trust: accepted=true successful=true");

            var confirmed = force;
            if (!force)
            {
                Console.Write("Save this memory entry? (y/N): ");
                var input = Console.ReadLine()?.Trim().ToLowerInvariant();
                confirmed = input == "y" || input == "yes";
            }

            if (!confirmed)
            {
                Console.WriteLine("Cancelled.");
                return;
            }

            await memoryService.AddKnownCommandAsync(command, generatedQuery, inferredTool, trusted: true);
            Console.WriteLine("Memory entry saved.");
        }, addCommandArg, addQueryOption, addToolOption, addForceOption);

        var listCommand = new Command("list", "List recent memory entries");
        var limitOption = new Option<int>("--limit", () => 20, "Maximum entries to show");
        listCommand.AddOption(limitOption);
        listCommand.SetHandler<int>(async (limit) =>
        {
            var memoryService = serviceProvider.GetRequiredService<IMemoryService>();
            var entries = await memoryService.ListAsync(limit);
            if (entries.Count == 0)
            {
                Console.WriteLine("Memory is empty.");
                return;
            }

            foreach (var entry in entries)
            {
                Console.WriteLine($"[{entry.TimestampUtc:O}] tool={entry.Tool} query=\"{entry.Query}\"");
                Console.WriteLine($"  command: {entry.Command}");
                Console.WriteLine($"  score: {entry.ConfidenceScore:0.00} accepted={entry.WasAccepted} successful={entry.WasSuccessful}");
            }
        }, limitOption);

        var clearCommand = new Command("clear", "Clear all memory entries");
        clearCommand.SetHandler(async () =>
        {
            var memoryService = serviceProvider.GetRequiredService<IMemoryService>();
            Console.Write("Clear all memory entries? (y/N): ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (input != "y" && input != "yes")
            {
                Console.WriteLine("Cancelled.");
                return;
            }

            await memoryService.ClearAsync();
            Console.WriteLine("Memory cleared.");
        });

        memoryCommand.AddCommand(addCommand);
        memoryCommand.AddCommand(listCommand);
        memoryCommand.AddCommand(clearCommand);
        return memoryCommand;
    }

    static Command BuildHiddenAliasCommand(string aliasName, string canonicalTool, ServiceProvider serviceProvider)
    {
        var aliasQueryArg = new Argument<string>("query", $"Natural language description of the {aliasName} operation");
        var aliasCommand = new Command(aliasName, $"Alias for {canonicalTool} command assistance")
        {
            IsHidden = true
        };
        aliasCommand.AddArgument(aliasQueryArg);
        aliasCommand.SetHandler<string>(async (query) =>
        {
            var request = new CommandRequest(canonicalTool, query, IsDirectCommand: true);
            await HandleCommandAsync(serviceProvider, request);
        }, aliasQueryArg);

        return aliasCommand;
    }

    static string InferToolFromCommand(string command)
    {
        return GetFirstToken(command)?.ToLowerInvariant() ?? "unknown";
    }

    static string? GetFirstToken(string command)
    {
        return command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    static CommandRequest ApplyToolHint(CommandRequest request)
    {
        if (!request.Tool.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return request;
        }

        var inferredTool = InferToolFromQuery(request.Query);
        if (string.IsNullOrWhiteSpace(inferredTool))
        {
            return request;
        }

        return request with { Tool = inferredTool };
    }

    static string? InferToolFromQuery(string query)
    {
        var lowered = query.ToLowerInvariant();
        var candidates = new[]
        {
            "git", "docker", "kubectl", "az", "azure", "npm", "yarn", "powershell", "pwsh", "ps"
        };

        foreach (var candidate in candidates)
        {
            if (lowered.Contains($"with {candidate}") ||
                lowered.Contains($"{candidate} ") ||
                lowered.Contains($" {candidate}") ||
                lowered.StartsWith(candidate + " ", StringComparison.Ordinal))
            {
                return candidate switch
                {
                    "powershell" or "pwsh" => "ps",
                    _ => candidate
                };
            }
        }

        return null;
    }

    static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Load .env file if it exists (check home directory first, then current directory)
        var homeEnvPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".env");
        var currentEnvPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        
        if (File.Exists(homeEnvPath))
        {
            Env.Load(homeEnvPath);
        }
        else if (File.Exists(currentEnvPath))
        {
            Env.Load(currentEnvPath);
        }
        
        // Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var aiConfig = new AIConfiguration();
        configuration.GetSection("AI").Bind(aiConfig);
        aiConfig.ApplyLegacyCompatibility();
        services.AddSingleton(aiConfig);
        var memoryConfig = new MemoryConfiguration();
        configuration.GetSection("Memory").Bind(memoryConfig);
        services.AddSingleton(memoryConfig);

        // Core services
        services.AddSingleton<IContextProvider, ContextProvider>();
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<ICommandRepository, InMemoryCommandRepository>();
        services.AddSingleton<ICommandValidator, CommandValidator>();
        services.AddSingleton<ILearningService, FileLearningService>();
        services.AddSingleton<IMemoryService, FileMemoryService>();
        services.AddSingleton<MemoryQueryGenerator>();
        services.AddSingleton<IMemoryQueryGenerator>(sp => sp.GetRequiredService<MemoryQueryGenerator>());
        services.AddSingleton<IMemoryQueryDiagnostics>(sp => sp.GetRequiredService<MemoryQueryGenerator>());
        services.AddSingleton<IToolAvailabilityService, ToolAvailabilityService>();
        services.AddSingleton<ICommandFallbackService, CommandFallbackService>();

        // HTTP clients for AI providers
        services.AddHttpClient<OpenAIProvider>();
        services.AddHttpClient<AzureOpenAIProvider>();
        services.AddHttpClient<AnthropicProvider>();
        services.AddHttpClient<GeminiProvider>();
        
        // AI providers
        services.AddSingleton<IAIProvider, OpenAIProvider>();
        services.AddSingleton<IAIProvider, AzureOpenAIProvider>();
        services.AddSingleton<IAIProvider, AnthropicProvider>();
        services.AddSingleton<IAIProvider, GeminiProvider>();
        
        // Pattern-based resolvers
        services.AddSingleton<GitCommandResolver>();
        services.AddSingleton<AzureCommandResolver>();
        services.AddSingleton<PatternCommandResolver>();
        
        // Main AI-powered resolver with multi-provider support
        services.AddSingleton<MultiProviderAICommandResolver>();
        services.AddSingleton<ICommandResolver>(sp => sp.GetRequiredService<MultiProviderAICommandResolver>());
        services.AddSingleton<IResolutionDiagnostics>(sp => sp.GetRequiredService<MultiProviderAICommandResolver>());

        return services;
    }
}
