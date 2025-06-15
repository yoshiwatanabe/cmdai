using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using System.Text;
using System.Text.Json;

namespace CmdAi.Core.Services;

public class AzureOpenAIProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly AIConfiguration _config;

    public AzureOpenAIProvider(HttpClient httpClient, AIConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        
        // Set authorization header if API key is provided
        if (!string.IsNullOrEmpty(config.AzureOpenAIApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AzureOpenAIApiKey);
        }
    }

    public string ModelName => _config.AzureOpenAIModelName ?? "model-router";

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_config.AzureOpenAIEndpoint) || 
                string.IsNullOrEmpty(_config.AzureOpenAIApiKey))
            {
                return false;
            }

            // Try a simple test request to check availability
            var testRequest = new
            {
                messages = new[]
                {
                    new { role = "user", content = "test" }
                },
                max_tokens = 1,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_config.AzureOpenAIEndpoint, content);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GenerateCommandAsync(string tool, string naturalLanguageQuery, string? context = null)
    {
        var prompt = BuildPrompt(tool, naturalLanguageQuery, context);
        
        var request = new
        {
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = 256,
            temperature = 0.1,
            top_p = 0.9,
            frequency_penalty = 0,
            presence_penalty = 0
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_config.AzureOpenAIEndpoint, content);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Azure OpenAI API returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);
            
            if (responseObj.TryGetProperty("choices", out var choices) && 
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentProperty))
                {
                    var command = contentProperty.GetString()?.Trim();
                    return ExtractCommand(command ?? "");
                }
            }

            throw new InvalidOperationException("Invalid response format from Azure OpenAI");
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException($"Azure OpenAI request timed out after {_config.TimeoutSeconds} seconds");
        }
    }

    private string BuildPrompt(string tool, string query, string? context)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are a CLI command generator. Convert natural language requests to precise CLI commands.");
        sb.AppendLine("IMPORTANT: Respond with ONLY the command, no explanations or additional text.");
        sb.AppendLine();
        
        // Add tool-specific context
        switch (tool.ToLowerInvariant())
        {
            case "git":
                sb.AppendLine("Tool: Git");
                sb.AppendLine("Examples:");
                sb.AppendLine("'check status' → git status");
                sb.AppendLine("'add all files' → git add .");
                sb.AppendLine("'commit with message' → git commit -m");
                sb.AppendLine("'undo last commit' → git reset --soft HEAD~1");
                sb.AppendLine("'delete untracked files' → git clean -fd");
                break;
                
            case "az":
            case "azure":
                sb.AppendLine("Tool: Azure CLI");
                sb.AppendLine("Examples:");
                sb.AppendLine("'list subscriptions' → az account list --output table");
                sb.AppendLine("'show current subscription' → az account show");
                sb.AppendLine("'list resource groups' → az group list --output table");
                sb.AppendLine("'list storage accounts' → az storage account list --output table");
                break;
                
            case "docker":
                sb.AppendLine("Tool: Docker");
                sb.AppendLine("Examples:");
                sb.AppendLine("'list containers' → docker ps");
                sb.AppendLine("'list images' → docker images");
                sb.AppendLine("'stop container' → docker stop");
                break;
                
            case "kubectl":
                sb.AppendLine("Tool: Kubernetes kubectl");
                sb.AppendLine("Examples:");
                sb.AppendLine("'list pods' → kubectl get pods");
                sb.AppendLine("'describe pod' → kubectl describe pod");
                sb.AppendLine("'get services' → kubectl get services");
                break;
        }
        
        if (!string.IsNullOrEmpty(context))
        {
            sb.AppendLine($"Context: {context}");
        }
        
        sb.AppendLine();
        sb.AppendLine($"Request: {query}");
        sb.AppendLine("Command:");
        
        return sb.ToString();
    }

    private string ExtractCommand(string response)
    {
        // Clean up the response to extract just the command
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip common prefixes
            if (trimmed.StartsWith("$") || trimmed.StartsWith(">") || trimmed.StartsWith("#"))
            {
                trimmed = trimmed.Substring(1).Trim();
            }
            
            // Skip explanatory text
            if (trimmed.Contains("command is") || trimmed.Contains("you can use") || 
                trimmed.Contains("this will") || trimmed.Length < 3)
            {
                continue;
            }
            
            // Return the first valid command line
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }
        }
        
        return response.Trim();
    }
}