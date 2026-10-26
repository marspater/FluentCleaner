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
    public async Task AnalyzeAsync_FindsFilesAndCalculatesBytes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "test1.tmp");
            var file2 = Path.Combine(tempDir, "test2.log");
            await File.WriteAllBytesAsync(file1, new byte[100]);
            await File.WriteAllBytesAsync(file2, new byte[250]);

            var service = new CleaningService();
            var entry = new CleanerEntry
            {
                Name = "Test Entry",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.tmp;*.log" }]
            };

            var scanResult = await service.AnalyzeAsync(entry);

            Assert.Equal(2, scanResult.FilesToDelete.Count);
            Assert.Equal(350, scanResult.TotalBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_RespectsExcludeKeys()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "keep.txt");
            var file2 = Path.Combine(tempDir, "delete.tmp");
            await File.WriteAllBytesAsync(file1, new byte[128]);
            await File.WriteAllBytesAsync(file2, new byte[64]);

            var service = new CleaningService();
            var entry = new CleanerEntry
            {
                Name = "Test Entry Exclusions",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.*" }],
                ExcludeKeys = [new ExcludeKeyEntry { Path = tempDir, Pattern = "keep.txt" }]
            };

            var scanResult = await service.AnalyzeAsync(entry);

            Assert.Single(scanResult.FilesToDelete);
            Assert.Contains(file2, scanResult.FilesToDelete);
            Assert.Equal(64, scanResult.TotalBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CleanAsync_DeletesQueuedFilesAndFreesBytes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "clean1.tmp");
            var file2 = Path.Combine(tempDir, "clean2.tmp");
            await File.WriteAllBytesAsync(file1, new byte[500]);
            await File.WriteAllBytesAsync(file2, new byte[300]);

            var service = new CleaningService();
            var entry = new CleanerEntry
            {
                Name = "Clean Test",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.tmp" }]
            };

            var scanResult = await service.AnalyzeAsync(entry);
            Assert.Equal(2, scanResult.FilesToDelete.Count);

            var (count, bytes) = await service.CleanAsync(scanResult);

            Assert.Equal(2, count);
            Assert.Equal(800, bytes);
            Assert.False(File.Exists(file1));
            Assert.False(File.Exists(file2));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
