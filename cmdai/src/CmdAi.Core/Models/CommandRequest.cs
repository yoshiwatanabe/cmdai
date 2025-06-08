namespace CmdAi.Core.Models;

public record CommandRequest(
    string Tool,
    string Query,
    bool IsDirectCommand = false);