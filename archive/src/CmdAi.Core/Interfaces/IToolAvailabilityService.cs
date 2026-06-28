namespace CmdAi.Core.Interfaces;

public interface IToolAvailabilityService
{
    bool IsAvailable(string binaryName);
}
