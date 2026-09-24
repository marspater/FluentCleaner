using FluentCleaner.Models;
using FluentCleaner.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CleaningServiceTests
{
    private readonly CleaningService _service = new();

    [Fact]
    public async Task AnalyzeAsync_FindsMatchingFiles_AndCalculatesTotalBytes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFilePath = Path.Combine(tempDir, "testfile.tmp");
            var content = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            await File.WriteAllBytesAsync(testFilePath, content);

            var entry = new CleanerEntry
            {
                Name = "Test App",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.tmp", Flag = FileKeyFlag.None }]
            };

            var result = await _service.AnalyzeAsync(entry);

            Assert.Single(result.FilesToDelete);
            Assert.Equal(testFilePath, result.FilesToDelete[0], StringComparer.OrdinalIgnoreCase);
            Assert.Equal(content.Length, result.TotalBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CleanAsync_DeletesScannedFiles_AndReturnsCountAndBytes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var testFilePath = Path.Combine(tempDir, "deleteme.log");
            var content = new byte[] { 10, 20, 30, 40 };
            await File.WriteAllBytesAsync(testFilePath, content);

            var entry = new CleanerEntry
            {
                Name = "Test App",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.log", Flag = FileKeyFlag.None }]
            };

            var scanResult = await _service.AnalyzeAsync(entry);
            Assert.Single(scanResult.FilesToDelete);

            var (deletedCount, bytesFreed) = await _service.CleanAsync(scanResult);

            Assert.Equal(1, deletedCount);
            Assert.Equal(content.Length, bytesFreed);
            Assert.False(File.Exists(testFilePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_SkipsExcludedFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var keepFile = Path.Combine(tempDir, "ignore.bak");
            var removeFile = Path.Combine(tempDir, "clean.tmp");
            await File.WriteAllBytesAsync(keepFile, new byte[] { 1, 2 });
            await File.WriteAllBytesAsync(removeFile, new byte[] { 3, 4, 5 });

            var entry = new CleanerEntry
            {
                Name = "Test App",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.*", Flag = FileKeyFlag.None }],
                ExcludeKeys = [new ExcludeKeyEntry { Type = ExcludeType.File, Path = tempDir, Pattern = "*.bak" }]
            };

            var result = await _service.AnalyzeAsync(entry);

            Assert.Single(result.FilesToDelete);
            Assert.Equal(removeFile, result.FilesToDelete[0], StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
