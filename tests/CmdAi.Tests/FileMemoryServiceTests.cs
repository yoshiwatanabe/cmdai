using CmdAi.Core.Models;
using CmdAi.Core.Services;
using Xunit;

namespace CmdAi.Tests;

public class FileMemoryServiceTests
{
    [Fact]
    public async Task FindBestMatchAsync_PrefersToolAlignedEntry()
    {
        var storePath = Path.Combine(Path.GetTempPath(), "cmdai-memory-test-" + Guid.NewGuid().ToString("N"));
        var config = new MemoryConfiguration
        {
            StorePath = storePath,
            CandidateCap = 50,
            HighConfidenceThreshold = 0.5
        };

        try
        {
            var service = new FileMemoryService(config);
            await service.RecordAsync(
                new CommandRequest("ps", "show source path for a command"),
                new CommandResult("Get-Command cmdai | Select-Object -ExpandProperty Source", "ps"),
                true,
                true);
            await service.RecordAsync(
                new CommandRequest("git", "show source path for a command"),
                new CommandResult("git remote -v", "git"),
                true,
                true);

            var match = await service.FindBestMatchAsync(new CommandRequest("ps", "show source path for given command"));

            Assert.NotNull(match);
            Assert.Contains("get-command", match!.Entry.Command, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(storePath))
            {
                Directory.Delete(storePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClearAsync_RemovesEntries()
    {
        var storePath = Path.Combine(Path.GetTempPath(), "cmdai-memory-test-" + Guid.NewGuid().ToString("N"));
        var config = new MemoryConfiguration { StorePath = storePath };

        try
        {
            var service = new FileMemoryService(config);
            await service.RecordAsync(
                new CommandRequest("auto", "find ts files"),
                new CommandResult("rg -n --glob \"*.ts\" \"CONFIG_ROOT\" .", "search"),
                true,
                true);

            var before = await service.ListAsync();
            Assert.NotEmpty(before);

            await service.ClearAsync();
            var after = await service.ListAsync();
            Assert.Empty(after);
        }
        finally
        {
            if (Directory.Exists(storePath))
            {
                Directory.Delete(storePath, recursive: true);
            }
        }
    }
}
