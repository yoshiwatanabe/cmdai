using CmdAi.Core.Services;
using Xunit;

namespace CmdAi.Tests;

public class AIProviderPromptHelperTests
{
    [Fact]
    public void ExtractCommand_ReturnsCommandFromCodeFence()
    {
        var response = """
            Here's the command:

            ```powershell
            Get-ChildItem -Recurse -File | Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-2) }
            ```
            """;

        var command = AIProviderPromptHelper.ExtractCommand(response);

        Assert.Equal("Get-ChildItem -Recurse -File | Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-2) }", command);
    }

    [Fact]
    public void ExtractCommand_ReturnsCommandAfterCommandPrefix()
    {
        var response = "Command: docker ps";

        var command = AIProviderPromptHelper.ExtractCommand(response);

        Assert.Equal("docker ps", command);
    }

    [Fact]
    public void ExtractCommand_SkipsProseAndReturnsCommandLine()
    {
        var response = """
            To list running containers, use:
            docker ps
            """;

        var command = AIProviderPromptHelper.ExtractCommand(response);

        Assert.Equal("docker ps", command);
    }
}
