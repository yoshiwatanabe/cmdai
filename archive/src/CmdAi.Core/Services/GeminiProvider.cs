using System.Net;
using System.Text;
using System.Text.Json;
using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;

namespace CmdAi.Core.Services;

public class GeminiProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly AIConfiguration _config;

    public GeminiProvider(HttpClient httpClient, AIConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.Gemini.TimeoutSeconds ?? config.TimeoutSeconds);
    }

    public string ProviderId => "gemini";
    public string ModelName => _config.Gemini.Model;

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
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                topP = 0.9,
                maxOutputTokens = 256
            }
        };

        AIProviderException? lastError = null;
        foreach (var apiKey in _config.Gemini.ApiKeys)
        {
            try
            {
                var endpoint = BuildEndpoint(apiKey);
                var json = JsonSerializer.Serialize(request);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
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
                if (responseObj.TryGetProperty("candidates", out var candidates) &&
                    candidates.ValueKind == JsonValueKind.Array &&
                    candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.ValueKind == JsonValueKind.Array &&
                        parts.GetArrayLength() > 0)
                    {
                        var firstPart = parts[0];
                        if (firstPart.TryGetProperty("text", out var text))
                        {
                            return AIProviderPromptHelper.ExtractCommand(text.GetString() ?? string.Empty);
                        }
                    }
                }

                throw new AIProviderException(ProviderId, ProviderFailureType.Unknown, "Invalid response format from Gemini");
            }
            catch (TaskCanceledException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Timeout, $"Gemini request timed out after {_httpClient.Timeout.TotalSeconds:0} seconds", innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                lastError = new AIProviderException(ProviderId, ProviderFailureType.Network, "Gemini network error", innerException: ex);
            }
        }

        throw lastError ?? new AIProviderException(ProviderId, ProviderFailureType.Unknown, "Gemini request failed");
    }

    private void ValidateConfiguration()
    {
        if (!_config.Gemini.Enabled)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Gemini provider is disabled");
        }

        if (string.IsNullOrWhiteSpace(_config.Gemini.Endpoint))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Gemini endpoint is not configured");
        }

        if (_config.Gemini.ApiKeys is null || _config.Gemini.ApiKeys.Length == 0)
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Gemini API key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_config.Gemini.Model))
        {
            throw new AIProviderException(ProviderId, ProviderFailureType.Configuration, "Gemini model is not configured");
        }
    }

    private string BuildEndpoint(string apiKey)
    {
        var endpoint = _config.Gemini.Endpoint;
        endpoint = endpoint.Replace("{model}", Uri.EscapeDataString(_config.Gemini.Model), StringComparison.OrdinalIgnoreCase);

        if (!endpoint.Contains("key=", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += endpoint.Contains('?') ? $"&key={Uri.EscapeDataString(apiKey)}" : $"?key={Uri.EscapeDataString(apiKey)}";
        }

        return endpoint;
    }

    private AIProviderException CreateExceptionFromStatus(HttpStatusCode statusCode, string message)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AIProviderException(ProviderId, ProviderFailureType.Authentication, $"Gemini authentication failed: {message}", statusCode),
            HttpStatusCode.TooManyRequests =>
                new AIProviderException(ProviderId, ProviderFailureType.RateLimit, $"Gemini rate limited request: {message}", statusCode),
            >= HttpStatusCode.InternalServerError =>
                new AIProviderException(ProviderId, ProviderFailureType.ServerError, $"Gemini server error: {message}", statusCode),
            _ =>
                new AIProviderException(ProviderId, ProviderFailureType.InvalidRequest, $"Gemini request rejected: {message}", statusCode)
        };
    }
}
