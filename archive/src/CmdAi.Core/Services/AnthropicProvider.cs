using System.Net;
using System.Text;
using System.Text.Json;
using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class AnthropicProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly AIConfiguration _config;

    public AnthropicProvider(HttpClient httpClient, AIConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.Anthropic.TimeoutSeconds ?? config.TimeoutSeconds);
    }

    public string ProviderId => "anthropic";
    public string ModelName => _config.Anthropic.Model;

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
        var request = new
        {
            model = _config.Anthropic.Model,
            max_tokens = 256,
            temperature = 0.1,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        AIProviderException? lastError = null;
        foreach (var apiKey in _config.Anthropic.ApiKeys)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.Anthropic.Endpoint);
                httpRequest.Headers.Add("x-api-key", apiKey);
                httpRequest.Headers.Add("anthropic-version", "2023-06-01");
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
                if (responseObj.TryGetProperty("content", out var contentArray) &&
                    contentArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in contentArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var type) &&
                            type.GetString() == "text" &&
                            item.TryGetProperty("text", out var text))
                        {
                            return AIProviderPromptHelper.ExtractCommand(text.GetString() ?? string.Empty);
                        }
                    }
                }

                throw new AIProviderException(ProviderId, ProviderFailureType.Unknown, "Invalid response format from Anthropic");
            }
            catch (TaskCanceledException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Timeout, $"Anthropic request timed out after {_httpClient.Timeout.TotalSeconds:0} seconds", innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Network, "Anthropic network error", innerException: ex);
            }
        }

        throw lastError ?? new AIProviderException(ProviderId, ProviderFailureType.Unknown, "Anthropic request failed");
    }

    private void ValidateConfiguration()
    {
        if (!_config.Anthropic.Enabled)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Anthropic provider is disabled");
        }

        if (string.IsNullOrWhiteSpace(_config.Anthropic.Endpoint))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Anthropic endpoint is not configured");
        }

        if (_config.Anthropic.ApiKeys is null || _config.Anthropic.ApiKeys.Length == 0)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Anthropic API key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_config.Anthropic.Model))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Anthropic model is not configured");
        }
    }

    private AIProviderException CreateExceptionFromStatus(HttpStatusCode statusCode, string message)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AIProviderException(ProviderId, ProviderFailureType.Authentication, $"Anthropic authentication failed: {message}", statusCode),
            HttpStatusCode.TooManyRequests =>
                new AIProviderException(ProviderId, ProviderFailureType.RateLimit, $"Anthropic rate limited request: {message}", statusCode),
            >= HttpStatusCode.InternalServerError =>
                new AIProviderException(ProviderId, ProviderFailureType.ServerError, $"Anthropic server error: {message}", statusCode),
            _ =>
                new AIProviderException(ProviderId, ProviderFailureType.InvalidRequest, $"Anthropic request rejected: {message}", statusCode)
        };
    }
}
