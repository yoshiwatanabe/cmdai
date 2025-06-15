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

        // Add custom version command (since --version is automatically handled)
        var versionCommand = new Command("version", "Show version information");
        versionCommand.SetHandler(() =>
        {
            ShowVersionInfo();
            return Task.CompletedTask;
        });
        
        rootCommand.AddCommand(versionCommand);

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

        var directToolCommands = new[] { "git", "az", "azure", "docker", "kubectl", "npm", "yarn" };
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

        rootCommand.AddCommand(askCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static void ShowVersionInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version?.ToString();
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "CmdAI";
        
        Console.WriteLine($"{title} v{informationalVersion}");
        Console.WriteLine("AI-powered CLI assistant with local Ollama integration");
        Console.WriteLine();
        Console.WriteLine("Repository: https://github.com/yoshiwatanabe/cmdai");
        Console.WriteLine("License: MIT");
    }

    static async Task ShowDiagnosticsAsync(ServiceProvider serviceProvider)
    {
        var aiConfig = serviceProvider.GetRequiredService<AIConfiguration>();
        var aiProviders = serviceProvider.GetServices<IAIProvider>();

        Console.WriteLine("=== CmdAI Diagnostics ===");
        Console.WriteLine();

        // Show configuration
        Console.WriteLine("📋 Configuration:");
        Console.WriteLine($"  AI Enabled: {aiConfig.EnableAI}");
        Console.WriteLine($"  Configured Providers: [{string.Join(", ", aiConfig.GetProviders())}]");
        Console.WriteLine($"  Fallback to Patterns: {aiConfig.FallbackToPatterns}");
        Console.WriteLine($"  Timeout: {aiConfig.TimeoutSeconds}s");
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

        // Show Azure OpenAI configuration
        Console.WriteLine("🌐 Azure OpenAI Configuration:");
        Console.WriteLine($"  Endpoint: {(string.IsNullOrEmpty(aiConfig.AzureOpenAIEndpoint) ? "Not configured" : "Configured")}");
        Console.WriteLine($"  API Key: {(string.IsNullOrEmpty(aiConfig.AzureOpenAIApiKey) ? "Not configured" : "Configured (***)")}");
        Console.WriteLine($"  Model: {aiConfig.AzureOpenAIModelName ?? "model-router"}");
        Console.WriteLine();

        // Show Ollama configuration
        Console.WriteLine("🖥️  Ollama Configuration:");
        Console.WriteLine($"  Endpoint: {aiConfig.OllamaEndpoint}");
        Console.WriteLine($"  Model: {aiConfig.ModelName}");
        Console.WriteLine();

        // Test provider connectivity
        Console.WriteLine("🔌 Provider Connectivity Tests:");
        foreach (var provider in aiProviders)
        {
            Console.Write($"  {provider.GetType().Name}: ");
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
        var orderedProviders = GetOrderedProvidersForDiagnostics(aiProviders, aiConfig);
        for (int i = 0; i < orderedProviders.Count(); i++)
        {
            var provider = orderedProviders.ElementAt(i);
            Console.WriteLine($"  {i + 1}. {provider.GetType().Name}");
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
    }

    static IEnumerable<IAIProvider> GetOrderedProvidersForDiagnostics(IEnumerable<IAIProvider> aiProviders, AIConfiguration config)
    {
        var configuredProviders = config.GetProviders();
        var orderedProviders = new List<IAIProvider>();

        foreach (var providerName in configuredProviders)
        {
            var provider = aiProviders.FirstOrDefault(p => 
                p.GetType().Name.ToLowerInvariant().Contains(providerName.ToLowerInvariant()));
            
            if (provider != null)
            {
                orderedProviders.Add(provider);
            }
        }

        var remainingProviders = aiProviders.Where(p => !orderedProviders.Contains(p));
        orderedProviders.AddRange(remainingProviders);

        return orderedProviders;
    }

    static async Task HandleCommandAsync(ServiceProvider serviceProvider, CommandRequest request)
    {
        var contextProvider = serviceProvider.GetRequiredService<IContextProvider>();
        var commandResolver = serviceProvider.GetRequiredService<ICommandResolver>();
        var commandExecutor = serviceProvider.GetRequiredService<ICommandExecutor>();
        var learningService = serviceProvider.GetRequiredService<ILearningService>();

        try
        {
            var context = await contextProvider.GetContextAsync();
            var result = await commandResolver.ResolveCommandAsync(request, context);

            if (result == null)
            {
                Console.WriteLine($"Sorry, I couldn't find a command for '{request.Query}' with {request.Tool}");
                return;
            }

            Console.WriteLine($"Suggested command: {result.Command}");
            Console.WriteLine($"Description: {result.Description}");

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
                await learningService.RecordFeedbackAsync(request, result, wasAccepted, wasSuccessful);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
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
        services.AddSingleton(aiConfig);

        // Core services
        services.AddSingleton<IContextProvider, ContextProvider>();
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<ICommandRepository, InMemoryCommandRepository>();
        services.AddSingleton<ICommandValidator, CommandValidator>();
        services.AddSingleton<ILearningService, FileLearningService>();

        // HTTP clients for AI providers
        services.AddHttpClient<OllamaAIProvider>();
        services.AddHttpClient<AzureOpenAIProvider>();
        
        // AI providers
        services.AddSingleton<IAIProvider, AzureOpenAIProvider>();
        services.AddSingleton<IAIProvider, OllamaAIProvider>();
        
        // Pattern-based resolvers
        services.AddSingleton<GitCommandResolver>();
        services.AddSingleton<AzureCommandResolver>();
        services.AddSingleton<PatternCommandResolver>();
        
        // Main AI-powered resolver with multi-provider support
        services.AddSingleton<ICommandResolver, MultiProviderAICommandResolver>();

        return services;
    }
}