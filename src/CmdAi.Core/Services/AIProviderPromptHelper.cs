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
                trimmed.Length < 3)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }
        }

        return response.Trim();
    }
}
