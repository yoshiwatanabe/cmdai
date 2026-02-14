using CmdAi.Core.Interfaces;

namespace CmdAi.Core.Services;

public class ToolAvailabilityService : IToolAvailabilityService
{
    public bool IsAvailable(string binaryName)
    {
        if (string.IsNullOrWhiteSpace(binaryName))
        {
            return false;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedDirectory = directory.Trim();
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(trimmedDirectory, binaryName + extension);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
