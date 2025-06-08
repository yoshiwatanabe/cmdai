using CmdAi.Core.Models;

namespace CmdAi.Core.Interfaces;

public interface ILearningService
{
    Task RecordFeedbackAsync(CommandRequest request, CommandResult result, bool wasAccepted, bool wasSuccessful);
    Task<IEnumerable<LearningEntry>> GetRelevantExamplesAsync(string tool, string query);
    Task OptimizeAsync();
}

public record LearningEntry(
    string Tool,
    string Query,
    string Command,
    DateTime Timestamp,
    bool WasAccepted,
    bool WasSuccessful,
    double ConfidenceScore = 1.0);