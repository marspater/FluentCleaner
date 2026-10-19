using System;
using System.IO;
using System.Threading.Tasks;
using FluentCleaner.Models;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CleaningServiceTests
{
    private readonly CleaningService _service = new();

    [Fact]
    public async Task AnalyzeAsync_EnumeratesAndCalculatesSize_ForValidFiles()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "FluentCleaner_Test_" + Guid.NewGuid().ToString("N"));
        var subFolder = Path.Combine(tempFolder, "SubDir");
        Directory.CreateDirectory(subFolder);

        var file1 = Path.Combine(tempFolder, "test1.tmp");
        var file2 = Path.Combine(subFolder, "test2.tmp");

        File.WriteAllText(file1, "Hello World 1!");
        File.WriteAllText(file2, "Hello World 22!");

        try
        {
            var entry = new CleanerEntry
            {
                Name = "Test Entry",
                FileKeys =
                {
                    new FileKeyEntry
                    {
                        Path = tempFolder,
                        Pattern = "*.tmp",
                        Flag = FileKeyFlag.Recurse
                    }
                }
            };

            var scanResult = await _service.AnalyzeAsync(entry);

            Assert.NotNull(scanResult);
            Assert.Equal(2, scanResult.FilesToDelete.Count);
            Assert.True(scanResult.TotalBytes > 0);
            Assert.Contains(file1, scanResult.FilesToDelete);
            Assert.Contains(file2, scanResult.FilesToDelete);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_RespectsExclusionRules()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "FluentCleaner_TestEx_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        var file1 = Path.Combine(tempFolder, "keep.tmp");
        var file2 = Path.Combine(tempFolder, "delete.tmp");

        File.WriteAllText(file1, "Keep me");
        File.WriteAllText(file2, "Delete me");

        try
        {
            var entry = new CleanerEntry
            {
                Name = "Test Entry Exclusion",
                FileKeys =
                {
                    new FileKeyEntry
                    {
                        Path = tempFolder,
                        Pattern = "*.tmp",
                        Flag = FileKeyFlag.None
                    }
                },
                ExcludeKeys =
                {
                    new ExcludeKeyEntry
                    {
                        Path = tempFolder,
                        Pattern = "keep.tmp",
                        Type = ExcludeType.File
                    }
                }
            };

            var scanResult = await _service.AnalyzeAsync(entry);

            Assert.NotNull(scanResult);
            Assert.Single(scanResult.FilesToDelete);
            Assert.Contains(file2, scanResult.FilesToDelete);
            Assert.DoesNotContain(file1, scanResult.FilesToDelete);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }
}
