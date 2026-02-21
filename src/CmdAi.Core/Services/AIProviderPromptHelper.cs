using System.Text;

namespace CmdAi.Core.Services;

internal static class AIProviderPromptHelper
{
    public static string BuildPrompt(string tool, string query, string? context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a CLI command generator. Convert natural language requests to precise CLI commands.");
        sb.AppendLine("IMPORTANT: Respond with ONLY the command, no explanations or additional text.");
        sb.AppendLine();

        switch (tool.ToLowerInvariant())
        {
            case "__memory_query__":
                sb.Clear();
                sb.AppendLine("You generate concise user intent queries from commands.");
                sb.AppendLine("Return exactly one short natural language query (5-12 words).");
                sb.AppendLine("Do not return a command.");
                sb.AppendLine("Do not include bullets, quotes, or extra text.");
                sb.AppendLine();
                sb.AppendLine($"Command: {query}");
                sb.AppendLine("Query:");
                return sb.ToString();
            case "git":
                sb.AppendLine("Tool: Git");
                sb.AppendLine("Examples:");
                sb.AppendLine("'check status' -> git status");
                sb.AppendLine("'add all files' -> git add .");
                sb.AppendLine("'commit with message' -> git commit -m");
                sb.AppendLine("'undo last commit' -> git reset --soft HEAD~1");
                sb.AppendLine("'delete untracked files' -> git clean -fd");
                break;
            case "az":
            case "azure":
                sb.AppendLine("Tool: Azure CLI");
                sb.AppendLine("Examples:");
                sb.AppendLine("'list subscriptions' -> az account list --output table");
                sb.AppendLine("'show current subscription' -> az account show");
                sb.AppendLine("'list resource groups' -> az group list --output table");
                break;
            case "docker":
                sb.AppendLine("Tool: Docker");
                sb.AppendLine("Examples:");
                sb.AppendLine("'list containers' -> docker ps");
                sb.AppendLine("'list images' -> docker images");
                break;
            case "kubectl":
                sb.AppendLine("Tool: Kubernetes kubectl");
                sb.AppendLine("Examples:");
                sb.AppendLine("'list pods' -> kubectl get pods");
                sb.AppendLine("'describe pod' -> kubectl describe pod");
                break;
            case "ps":
            case "pwsh":
            case "powershell":
                sb.AppendLine("Tool: PowerShell");
                sb.AppendLine("Examples:");
                sb.AppendLine("'show source path for a command' -> Get-Command git | Select-Object -ExpandProperty Source");
                sb.AppendLine("'list running processes' -> Get-Process");
                sb.AppendLine("'search PATH for an exe' -> Get-Command dotnet | Format-List Source");
                break;
        }

        if (!string.IsNullOrWhiteSpace(context))
        {
            sb.AppendLine($"Context: {context}");
        }

        sb.AppendLine();
        sb.AppendLine($"Request: {query}");
        sb.AppendLine("Command:");

        return sb.ToString();
    }

    public static string ExtractCommand(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        var codeBlockCommand = TryExtractFromCodeFence(response);
        if (!string.IsNullOrWhiteSpace(codeBlockCommand))
        {
            return codeBlockCommand;
        }

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("$") || trimmed.StartsWith(">") || trimmed.StartsWith("#"))
            {
                trimmed = trimmed[1..].Trim();
            }

            if (trimmed.Contains("command is", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("you can use", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("this will", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("here's", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Length < 3)
            {
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal) ||
                trimmed.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("bash", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("shell", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trimmed.EndsWith(':') &&
                (trimmed.Contains("use", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains("command", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (trimmed.StartsWith("Command:", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = trimmed["Command:".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }
        }

        return response.Trim();
    }

    private static string? TryExtractFromCodeFence(string response)
    {
        var startFence = response.IndexOf("```", StringComparison.Ordinal);
        if (startFence < 0)
        {
            return null;
        }

        var afterStart = response.IndexOf('\n', startFence);
        if (afterStart < 0)
        {
            return null;
        }

        var endFence = response.IndexOf("```", afterStart + 1, StringComparison.Ordinal);
        if (endFence < 0)
        {
            return null;
        }

        var block = response[(afterStart + 1)..endFence];
        var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.StartsWith("$") || trimmed.StartsWith(">") || trimmed.StartsWith("#"))
            {
                trimmed = trimmed[1..].Trim();
            }

            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        return null;
    }
}
