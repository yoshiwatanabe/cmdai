namespace CmdAi.Core.Models;

public class MemoryConfiguration
{
    public string? StorePath { get; set; }
    public int CandidateCap { get; set; } = 200;
    public int ListLimitDefault { get; set; } = 50;
    public double HighConfidenceThreshold { get; set; } = 0.62;
    public bool EnableRedaction { get; set; } = false;
    public string[] RedactionAllowlist { get; set; } = [];
}

public record MemoryEntry(
    string EntryId,
    string Tool,
    string Query,
    string Command,
    DateTime TimestampUtc,
    bool WasAccepted,
    bool WasSuccessful,
    double ConfidenceScore,
    string ContentHash,
    string MachineId);

public record MemoryMatch(
    MemoryEntry Entry,
    double Score,
    string Reason,
    bool IsHighConfidence);

public record MemoryEvent(
    string EventId,
    string MachineId,
    DateTime TimestampUtc,
    string Tool,
    string Query,
    string Command,
    bool WasAccepted,
    bool WasSuccessful,
    string ContentHash,
    double ConfidenceScore);
