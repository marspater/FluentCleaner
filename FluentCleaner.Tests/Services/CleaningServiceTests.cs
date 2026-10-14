using System;
using System.IO;
using System.Threading.Tasks;
using FluentCleaner.Models;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CleaningServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_CalculatesFileSizesAndFindsMatchingFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        try
        {
            var file1 = Path.Combine(tempDir, "file1.tmp");
            var file2 = Path.Combine(subDir, "file2.tmp");
            var file3 = Path.Combine(subDir, "file3.txt"); // non-matching pattern

            File.WriteAllBytes(file1, new byte[100]);
            File.WriteAllBytes(file2, new byte[200]);
            File.WriteAllBytes(file3, new byte[300]);

            var entry = new CleanerEntry
            {
                Name = "Test App",
                FileKeys =
                [
                    new FileKeyEntry
                    {
                        Path = tempDir,
                        Pattern = "*.tmp",
                        Flag = FileKeyFlag.Recurse
                    }
                ]
            };

            var service = new CleaningService();
            var scanResult = await service.AnalyzeAsync(entry);

            Assert.Equal(2, scanResult.FilesToDelete.Count);
            Assert.Contains(file1, scanResult.FilesToDelete);
            Assert.Contains(file2, scanResult.FilesToDelete);
            Assert.DoesNotContain(file3, scanResult.FilesToDelete);
            Assert.Equal(300, scanResult.TotalBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CleanAsync_DeletesScannedFilesAndFreesBytes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "log1.log");
            File.WriteAllBytes(file1, new byte[500]);

            var entry = new CleanerEntry
            {
                Name = "Test Log Cleaner",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.log" }]
            };

            var service = new CleaningService();
            var scanResult = await service.AnalyzeAsync(entry);

            Assert.Single(scanResult.FilesToDelete);
            Assert.Equal(500, scanResult.TotalBytes);

            var (deletedCount, freedBytes) = await service.CleanAsync(scanResult);

            Assert.Equal(1, deletedCount);
            Assert.Equal(500, freedBytes);
            Assert.False(File.Exists(file1));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
