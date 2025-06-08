using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using CmdAi.Core.Services;

namespace CmdAi.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = ConfigureServices();
        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = new RootCommand("AI-powered CLI assistant that translates natural language to CLI commands");

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

        var directToolCommands = new[] { "git", "az" };
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

    static async Task HandleCommandAsync(ServiceProvider serviceProvider, CommandRequest request)
    {
        var contextProvider = serviceProvider.GetRequiredService<IContextProvider>();
        var commandResolver = serviceProvider.GetRequiredService<ICommandResolver>();
        var commandExecutor = serviceProvider.GetRequiredService<ICommandExecutor>();

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

            if (result.RequiresConfirmation)
            {
                var confirmed = await commandExecutor.ConfirmExecutionAsync(result);
                if (confirmed)
                {
                    await commandExecutor.ExecuteAsync(result, context);
                }
                else
                {
                    Console.WriteLine("Command execution cancelled.");
                }
            }
            else
            {
                await commandExecutor.ExecuteAsync(result, context);
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
        
        services.AddSingleton<IContextProvider, ContextProvider>();
        services.AddSingleton<ICommandResolver, GitCommandResolver>();
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<ICommandRepository, InMemoryCommandRepository>();

        return services;
    }
}