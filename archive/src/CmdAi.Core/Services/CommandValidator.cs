using CmdAi.Core.Interfaces;
using System.Text.RegularExpressions;

namespace CmdAi.Core.Services;

public class CommandValidator : ICommandValidator
{
    private readonly List<string> _dangerousPatterns;
    private readonly Dictionary<string, List<string>> _toolSpecificDangerousPatterns;

    public CommandValidator()
    {
        _dangerousPatterns = new List<string>
        {
            // Destructive operations
            @"\brm\s+.*-rf?\s+/",                    // rm -rf /
            @"\brm\s+.*-rf?\s+\*",                   // rm -rf *
            @"\bmkfs\b",                             // Format filesystem
            @"\bdd\s+.*of=/dev/",                    // Write to device
            @">\s*/dev/sd[a-z]",                     // Redirect to disk
            @"\bshred\b",                            // Secure delete
            @"\b:\(\)\{\s*:\|:\&\s*\}",             // Fork bomb
            
            // Network/Security risks
            @"\bcurl\s+.*\|\s*bash",                 // Pipe curl to bash
            @"\bwget\s+.*\|\s*bash",                 // Pipe wget to bash
            @"\bchmod\s+777",                        // Open permissions
            @"\bchown\s+.*root",                     // Change ownership to root
            @"\bsudo\s+.*rm\s+.*-rf",               // Sudo destructive
            
            // System modification
            @"\b/etc/passwd",                        // Password file
            @"\b/etc/shadow",                        // Shadow file
            @"\binit\s+0",                          // Shutdown
            @"\bshutdown\b",                        // Shutdown
            @"\breboot\b",                          // Reboot
            @"\bhalt\b",                            // Halt system
        };

        _toolSpecificDangerousPatterns = new Dictionary<string, List<string>>
        {
            ["git"] = new List<string>
            {
                @"\bgit\s+.*--force",                // Force operations
                @"\bgit\s+.*-f\b",                   // Force flag
                @"\bgit\s+clean\s+.*-fd",           // Force clean
                @"\bgit\s+reset\s+.*--hard\s+HEAD~[5-9]", // Large hard resets
            },
            ["az"] = new List<string>
            {
                @"\baz\s+.*delete\s+.*--yes\s+.*--no-wait", // Dangerous Azure deletes
                @"\baz\s+group\s+delete\s+.*--yes",         // Resource group delete
                @"\baz\s+vm\s+delete\s+.*--yes",            // VM delete
            },
            ["docker"] = new List<string>
            {
                @"\bdocker\s+.*--privileged",       // Privileged containers
                @"\bdocker\s+.*-v\s+/:/",          // Mount root
                @"\bdocker\s+system\s+prune\s+.*-a", // Prune all
            },
            ["kubectl"] = new List<string>
            {
                @"\bkubectl\s+delete\s+.*--all",    // Delete all resources
                @"\bkubectl\s+.*--force",           // Force operations
            }
        };
    }

    public Task<CommandValidationResult> ValidateCommandAsync(string command, string tool)
    {
        var warnings = new List<string>();
        var isValid = true;
        var isSafe = true;
        string? validationMessage = null;

        // Check for empty or invalid commands
        if (string.IsNullOrWhiteSpace(command))
        {
            return Task.FromResult(new CommandValidationResult(false, true, "Command cannot be empty"));
        }

        // Check for dangerous patterns
        if (!IsSafeCommand(command))
        {
            isSafe = false;
            validationMessage = "Command contains potentially dangerous patterns";
            warnings.Add("This command may be destructive or unsafe");
        }

        // Check tool-specific dangerous patterns
        if (_toolSpecificDangerousPatterns.ContainsKey(tool.ToLowerInvariant()))
        {
            var toolPatterns = _toolSpecificDangerousPatterns[tool.ToLowerInvariant()];
            foreach (var pattern in toolPatterns)
            {
                if (Regex.IsMatch(command, pattern, RegexOptions.IgnoreCase))
                {
                    warnings.Add($"Command contains {tool}-specific risky operation");
                    break;
                }
            }
        }

        // Basic command structure validation
        if (!ValidateCommandStructure(command, tool))
        {
            isValid = false;
            validationMessage = $"Command does not appear to be a valid {tool} command";
        }

        return Task.FromResult(new CommandValidationResult(isValid, isSafe, validationMessage, warnings));
    }

    public bool IsSafeCommand(string command)
    {
        foreach (var pattern in _dangerousPatterns)
        {
            if (Regex.IsMatch(command, pattern, RegexOptions.IgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    public IEnumerable<string> GetDangerousPatterns()
    {
        return _dangerousPatterns.AsReadOnly();
    }

    private bool ValidateCommandStructure(string command, string tool)
    {
        var trimmedCommand = command.Trim();
        
        return tool.ToLowerInvariant() switch
        {
            "git" => trimmedCommand.StartsWith("git ", StringComparison.OrdinalIgnoreCase),
            "az" or "azure" => trimmedCommand.StartsWith("az ", StringComparison.OrdinalIgnoreCase),
            "docker" => trimmedCommand.StartsWith("docker ", StringComparison.OrdinalIgnoreCase),
            "kubectl" => trimmedCommand.StartsWith("kubectl ", StringComparison.OrdinalIgnoreCase),
            _ => true // Allow unknown tools
        };
    }
}