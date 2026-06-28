namespace CmdAi.Core.Interfaces;

public interface IMemoryQueryGenerator
{
    Task<string> GenerateShortQueryAsync(string tool, string command);
}
