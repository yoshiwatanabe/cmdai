using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Http.Headers;

namespace CmdAi.Core.Services;

public class AzureOpenAIProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly AIConfiguration _config;

    public AzureOpenAIProvider(HttpClient httpClient, AIConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.AzureOpenAI.TimeoutSeconds ?? config.TimeoutSeconds);
    }

    public string ProviderId => "azureopenai";
    public string ModelName => _config.AzureOpenAI.Model;

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            if (!_config.AzureOpenAI.Enabled)
            {
                return false;
            }

            ValidateConfiguration();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GenerateCommandAsync(string tool, string naturalLanguageQuery, string? context = null)
    {
        ValidateConfiguration();
        var prompt = AIProviderPromptHelper.BuildPrompt(tool, naturalLanguageQuery, context);
        var endpoint = BuildEndpoint();
        var request = new
        {
            model = _config.AzureOpenAI.Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        AIProviderException? lastError = null;
        var json = JsonSerializer.Serialize(request);

        foreach (var apiKey in _config.AzureOpenAI.ApiKeys)
        {
            try
            {
                return await SendWithBearerAuthAsync(endpoint, json, apiKey);
            }
            catch (AIProviderException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                try
                {
                    return await SendWithApiKeyAuthAsync(endpoint, json, apiKey);
                }
                catch (AIProviderException fallbackEx)
                {
                    lastError = fallbackEx;

                    if (fallbackEx.FailureType == ProviderFailureType.Authentication ||
                        fallbackEx.FailureType == ProviderFailureType.RateLimit ||
                        fallbackEx.FailureType == ProviderFailureType.ServerError)
                    {
                        continue;
                    }

                    throw;
                }
            }
            catch (AIProviderException ex)
            {
                lastError = ex;

                if (ex.FailureType == ProviderFailureType.Authentication ||
                    ex.FailureType == ProviderFailureType.RateLimit ||
                    ex.FailureType == ProviderFailureType.ServerError)
                {
                    continue;
                }

                throw;
            }
            catch (TaskCanceledException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Timeout, $"Azure OpenAI request timed out after {_httpClient.Timeout.TotalSeconds:0} seconds", innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Network, "Azure OpenAI network error", innerException: ex);
            }
        }

        throw lastError ?? new AIProviderException(ProviderId, ProviderFailureType.Unknown, "Azure OpenAI request failed");
    }

    private async Task<string> SendWithBearerAuthAsync(string endpoint, string json, string apiKey)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await SendRequestAsync(httpRequest);
    }

    private async Task<string> SendWithApiKeyAuthAsync(string endpoint, string json, string apiKey)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("api-key", apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await SendRequestAsync(httpRequest);
    }

    private async Task<string> SendRequestAsync(HttpRequestMessage httpRequest)
    {
        using var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw CreateExceptionFromStatus(response.StatusCode, message);
        }

        var responseText = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);
        if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentProperty))
            {
                var command = contentProperty.GetString()?.Trim();
                return AIProviderPromptHelper.ExtractCommand(command ?? string.Empty);
            }
        }

        throw new AIProviderException(ProviderId, ProviderFailureType.Unknown, "Invalid response format from Azure OpenAI");
    }

    private string BuildEndpoint()
    {
        var endpoint = _config.AzureOpenAI.Endpoint.Trim();
        if (endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        endpoint = endpoint.TrimEnd('/');
        return $"{endpoint}/chat/completions";
    }

    private void ValidateConfiguration()
    {
        if (!_config.AzureOpenAI.Enabled)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Azure OpenAI provider is disabled");
        }

        if (string.IsNullOrWhiteSpace(_config.AzureOpenAI.Endpoint))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Azure OpenAI endpoint is not configured (expected Foundry/OpenAI-v1 base URL)");
        }

        if (_config.AzureOpenAI.ApiKeys is null || _config.AzureOpenAI.ApiKeys.Length == 0)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Azure OpenAI API key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_config.AzureOpenAI.Model))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Azure OpenAI model is not configured (expected deployment/model name for Foundry)");
        }
    }

    private AIProviderException CreateExceptionFromStatus(HttpStatusCode statusCode, string message)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AIProviderException(ProviderId, ProviderFailureType.Authentication, $"Azure OpenAI authentication failed: {message}", statusCode),
            HttpStatusCode.TooManyRequests =>
                new AIProviderException(ProviderId, ProviderFailureType.RateLimit, $"Azure OpenAI rate limited request: {message}", statusCode),
            >= HttpStatusCode.InternalServerError =>
                new AIProviderException(ProviderId, ProviderFailureType.ServerError, $"Azure OpenAI server error: {message}", statusCode),
            _ =>
                new AIProviderException(ProviderId, ProviderFailureType.InvalidRequest, $"Azure OpenAI request rejected: {message}", statusCode)
        };
    }
}
