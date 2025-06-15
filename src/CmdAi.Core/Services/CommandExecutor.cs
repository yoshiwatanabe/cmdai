using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using System.Diagnostics;

namespace CmdAi.Core.Services;

public class CommandExecutor : ICommandExecutor
{
    public Task<bool> ConfirmExecutionAsync(CommandResult command)
    {
        Console.WriteLine();
        Console.Write($"Execute '{command.Command}'? (y/N): ");
        
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        return Task.FromResult(input == "y" || input == "yes");
    }

    public async Task<bool> ExecuteAsync(CommandResult command, CommandContext context)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Command}\"",
                WorkingDirectory = context.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (context.Environment != null)
            {
                foreach (var env in context.Environment)
                {
                    processInfo.Environment[env.Key] = env.Value;
                }
            }

            using var process = new Process { StartInfo = processInfo };
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.Error.WriteLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Command completed successfully.");
                return true;
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"Command failed with exit code {process.ExitCode}.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing command: {ex.Message}");
            return false;
        }
    }
}