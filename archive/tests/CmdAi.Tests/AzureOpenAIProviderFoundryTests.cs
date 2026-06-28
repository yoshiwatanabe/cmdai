using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CmdAi.Core.Models;
using CmdAi.Core.Services;
using Xunit;

namespace CmdAi.Tests;

public class AzureOpenAIProviderFoundryTests
{
    [Fact]
    public async Task GenerateCommandAsync_AppendsChatCompletions_ForBaseUrlWithTrailingSlash()
    {
        var handler = new QueueHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("git status")
            });

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(httpClient, "https://example.openai.azure.com/openai/v1/");

        var command = await provider.GenerateCommandAsync("git", "show status");

        Assert.Equal("git status", command);
        Assert.Single(handler.Requests);
        Assert.Equal("https://example.openai.azure.com/openai/v1/chat/completions", handler.Requests[0].Url);
    }

    [Fact]
    public async Task GenerateCommandAsync_AppendsChatCompletions_ForBaseUrlWithoutTrailingSlash()
    {
        var handler = new QueueHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("git status")
            });

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(httpClient, "https://example.openai.azure.com/openai/v1");

        await provider.GenerateCommandAsync("git", "show status");

        Assert.Single(handler.Requests);
        Assert.Equal("https://example.openai.azure.com/openai/v1/chat/completions", handler.Requests[0].Url);
    }

    [Fact]
    public async Task GenerateCommandAsync_UsesProvidedChatCompletionsEndpointAsIs()
    {
        var endpoint = "https://example.openai.azure.com/openai/v1/chat/completions?api-version=preview";
        var handler = new QueueHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("git status")
            });

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(httpClient, endpoint);

        await provider.GenerateCommandAsync("git", "show status");

        Assert.Single(handler.Requests);
        Assert.Equal(endpoint, handler.Requests[0].Url);
    }

    [Fact]
    public async Task GenerateCommandAsync_UsesBearerAuth_First()
    {
        var handler = new QueueHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("git status")
            });

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(httpClient, "https://example.openai.azure.com/openai/v1/");

        await provider.GenerateCommandAsync("git", "show status");

        Assert.Single(handler.Requests);
        Assert.Equal("Bearer", handler.Requests[0].AuthorizationScheme);
        Assert.False(handler.Requests[0].HasApiKeyHeader);
    }

    [Fact]
    public async Task GenerateCommandAsync_FallsBackToApiKey_WhenBearerUnauthorized()
    {
        var responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"message\":\"unauthorized\"}}", Encoding.UTF8, "application/json")
        });
        responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("git status")
        });
        var handler = new QueueHttpMessageHandler(_ => responses.Dequeue().Invoke(_));

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(httpClient, "https://example.openai.azure.com/openai/v1/");

        var command = await provider.GenerateCommandAsync("git", "show status");

        Assert.Equal("git status", command);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Bearer", handler.Requests[0].AuthorizationScheme);
        Assert.False(handler.Requests[0].HasApiKeyHeader);
        Assert.True(handler.Requests[1].HasApiKeyHeader);
        Assert.Null(handler.Requests[1].AuthorizationScheme);
    }

    [Fact]
    public async Task GenerateCommandAsync_ThrowsConfigurationError_WhenModelMissing()
    {
        var config = new AIConfiguration
        {
            AzureOpenAI = new ProviderConfiguration
            {
                Enabled = true,
                Endpoint = "https://example.openai.azure.com/openai/v1/",
                Model = "",
                ApiKeys = ["test-key"]
            }
        };
        using var httpClient = new HttpClient(new QueueHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var provider = new AzureOpenAIProvider(httpClient, config);

        var ex = await Assert.ThrowsAsync<AIProviderException>(() => provider.GenerateCommandAsync("git", "show status"));
        Assert.Equal(ProviderFailureType.Configuration, ex.FailureType);
    }

    private static AzureOpenAIProvider CreateProvider(HttpClient httpClient, string endpoint)
    {
        var config = new AIConfiguration
        {
            TimeoutSeconds = 10,
            AzureOpenAI = new ProviderConfiguration
            {
                Enabled = true,
                Endpoint = endpoint,
                Model = "DeepSeek-R1-0528",
                ApiKeys = ["test-key"]
            }
        };

        return new AzureOpenAIProvider(httpClient, config);
    }

    private static StringContent JsonContent(string command)
    {
        return new StringContent(
            $"{{\"choices\":[{{\"message\":{{\"content\":\"{command}\"}}}}]}}",
            Encoding.UTF8,
            "application/json");
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public QueueHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<RequestSnapshot> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var hasApiKey = request.Headers.Contains("api-key");
            var auth = request.Headers.Authorization;
            Requests.Add(new RequestSnapshot(
                request.RequestUri?.ToString() ?? string.Empty,
                hasApiKey,
                auth?.Scheme));

            return Task.FromResult(_responder(request));
        }
    }

    private sealed record RequestSnapshot(string Url, bool HasApiKeyHeader, string? AuthorizationScheme);
}
