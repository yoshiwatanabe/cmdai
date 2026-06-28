using System.Text.RegularExpressions;
using CmdAi.Core.Interfaces;

namespace CmdAi.Core.Services;

public class CommandFallbackService : ICommandFallbackService
{
    public string? GetFallbackCommand(string primaryCommand)
    {
        if (string.IsNullOrWhiteSpace(primaryCommand))
        {
            return null;
        }

        var trimmed = primaryCommand.Trim();
        if (!trimmed.StartsWith("rg ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var patternMatch = Regex.Match(trimmed, "\"([^\"]+)\"");
        var includeMatch = Regex.Match(trimmed, @"-g\s+""([^""]+)""");
        var searchRoot = ".";

        if (!patternMatch.Success)
        {
            return "grep -RIn .";
        }

        var pattern = patternMatch.Groups[1].Value;
        if (includeMatch.Success)
        {
            var includePattern = includeMatch.Groups[1].Value;
            return $"grep -RIn --include=\"{includePattern}\" \"{pattern}\" {searchRoot}";
        }

        return $"grep -RIn \"{pattern}\" {searchRoot}";
    }
}
