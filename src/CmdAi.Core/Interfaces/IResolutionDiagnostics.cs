using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface IResolutionDiagnostics
{
    IReadOnlyList<ProviderAttemptDiagnostics> GetLastResolutionTrace();
}
