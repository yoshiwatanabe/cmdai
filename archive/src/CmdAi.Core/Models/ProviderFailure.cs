using System.Net;

namespace CmdAi.Core.Models;

public enum ProviderFailureType
{
    Timeout,
    Network,
    RateLimit,
    ServerError,
    Authentication,
    InvalidRequest,
    Configuration,
    Unknown
}

public sealed class AIProviderException : Exception
{
    public AIProviderException(
        string providerId,
        ProviderFailureType failureType,
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null) : base(message, innerException)
    {
        ProviderId = providerId;
        FailureType = failureType;
        StatusCode = statusCode;
    }

    public string ProviderId { get; }
    public ProviderFailureType FailureType { get; }
    public HttpStatusCode? StatusCode { get; }
    public bool IsTransient =>
        FailureType == ProviderFailureType.Timeout ||
        FailureType == ProviderFailureType.Network ||
        FailureType == ProviderFailureType.RateLimit ||
        FailureType == ProviderFailureType.ServerError;
}
