namespace CmdAi.Core.Models;

public record ProviderAttemptDiagnostics(
    string ProviderId,
    string ModelName,
    bool Attempted,
    bool Succeeded,
    ProviderFailureType? FailureType = null,
    string? Message = null);
