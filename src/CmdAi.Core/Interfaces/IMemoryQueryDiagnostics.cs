using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface IMemoryQueryDiagnostics
{
    IReadOnlyList<ProviderAttemptDiagnostics> GetLastMemoryQueryTrace();
}
