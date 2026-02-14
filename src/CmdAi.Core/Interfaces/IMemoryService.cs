using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface IMemoryService
{
    Task<MemoryMatch?> FindBestMatchAsync(CommandRequest request);
    Task RecordAsync(CommandRequest request, CommandResult result, bool wasAccepted, bool wasSuccessful);
    Task<IReadOnlyList<MemoryEntry>> ListAsync(int? limit = null);
    Task ClearAsync();
}
