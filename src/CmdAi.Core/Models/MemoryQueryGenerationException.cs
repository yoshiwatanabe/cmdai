namespace CmdAi.Core.Models;

public sealed class MemoryQueryGenerationException : Exception
{
    public MemoryQueryGenerationException(string message, IReadOnlyList<ProviderAttemptDiagnostics> attempts)
        : base(message)
    {
        Attempts = attempts;
    }

    public IReadOnlyList<ProviderAttemptDiagnostics> Attempts { get; }
}
