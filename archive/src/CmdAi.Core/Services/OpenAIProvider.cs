using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class OpenAIProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly AIConfiguration _config;

    public OpenAIProvider(HttpClient httpClient, AIConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.OpenAI.TimeoutSeconds ?? config.TimeoutSeconds);
    }

    public string ProviderId => "openai";
    public string ModelName => _config.OpenAI.Model;

    public Task<bool> IsAvailableAsync()
    {
        try
        {
            ValidateConfiguration();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<string> GenerateCommandAsync(string tool, string naturalLanguageQuery, string? context = null)
    {
        ValidateConfiguration();
        var prompt = AIProviderPromptHelper.BuildPrompt(tool, naturalLanguageQuery, context);
        var maxCompletionTokens = tool.Equals("__memory_query__", StringComparison.OrdinalIgnoreCase)
            ? 2048
            : 512;
        var request = new
        {
            model = _config.OpenAI.Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            // gpt-5 family models can return empty content with finish_reason=length at low caps.
            max_completion_tokens = maxCompletionTokens
        };

        AIProviderException? lastError = null;
        foreach (var apiKey in _config.OpenAI.ApiKeys)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.OpenAI.Endpoint);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                if (!response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadAsStringAsync();
                    var error = CreateExceptionFromStatus(response.StatusCode, message);
                    lastError = error;

                    if (error.FailureType == ProviderFailureType.Authentication ||
                        error.FailureType == ProviderFailureType.RateLimit ||
                        error.FailureType == ProviderFailureType.ServerError)
                    {
                        continue;
                    }

                    throw error;
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);
                if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var contentProperty))
                    {
                        var command = contentProperty.GetString() ?? string.Empty;
                        return AIProviderPromptHelper.ExtractCommand(command);
                    }
                }

                throw new AIProviderException(ProviderId, ProviderFailureType.Unknown, "Invalid response format from OpenAI");
            }
            catch (TaskCanceledException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Timeout, $"OpenAI request timed out after {_httpClient.Timeout.TotalSeconds:0} seconds", innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Network, "OpenAI network error", innerException: ex);
            }
        }

        throw lastError ?? new AIProviderException(ProviderId, ProviderFailureType.Unknown, "OpenAI request failed");
    }

    private void ValidateConfiguration()
    {
        if (!_config.OpenAI.Enabled)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "OpenAI provider is disabled");
        }

        if (string.IsNullOrWhiteSpace(_config.OpenAI.Endpoint))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "OpenAI endpoint is not configured");
        }

        if (_config.OpenAI.ApiKeys is null || _config.OpenAI.ApiKeys.Length == 0)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "OpenAI API key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_config.OpenAI.Model))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "OpenAI model is not configured");
        }
    }

    private AIProviderException CreateExceptionFromStatus(HttpStatusCode statusCode, string message)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AIProviderException(ProviderId, ProviderFailureType.Authentication, $"OpenAI authentication failed: {message}", statusCode),
            HttpStatusCode.TooManyRequests =>
                new AIProviderException(ProviderId, ProviderFailureType.RateLimit, $"OpenAI rate limited request: {message}", statusCode),
            >= HttpStatusCode.InternalServerError =>
                new AIProviderException(ProviderId, ProviderFailureType.ServerError, $"OpenAI server error: {message}", statusCode),
            _ =>
                new AIProviderException(ProviderId, ProviderFailureType.InvalidRequest, $"OpenAI request rejected: {message}", statusCode)
        };
    }
}
