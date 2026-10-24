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
    public async Task AnalyzeAsync_FindsMatchingFilesAndCalculatesBytes()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "FC_CleaningServiceTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var file1 = Path.Combine(tempFolder, "test1.tmp");
            var file2 = Path.Combine(tempFolder, "test2.log");
            File.WriteAllText(file1, "Hello World"); // 11 bytes
            File.WriteAllText(file2, "1234567890");  // 10 bytes

            var entry = new CleanerEntry
            {
                Name = "Test Entry",
                FileKeys = { FileKeyEntry.Parse($@"{tempFolder}|*.tmp;*.log") }
            };

            var scanResult = await _service.AnalyzeAsync(entry);

            Assert.Equal(2, scanResult.FilesToDelete.Count);
            Assert.Equal(21, scanResult.TotalBytes);
            Assert.Contains(file1, scanResult.FilesToDelete);
            Assert.Contains(file2, scanResult.FilesToDelete);
            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_RecursesSubdirectories_WhenRecurseFlagIsSet()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "FC_CleaningServiceRecurse_" + Guid.NewGuid().ToString("N"));
        var subFolder = Path.Combine(tempFolder, "SubDir");
        Directory.CreateDirectory(subFolder);

        try
        {
            var rootFile = Path.Combine(tempFolder, "root.tmp");
            var subFile = Path.Combine(subFolder, "sub.tmp");
            File.WriteAllText(rootFile, "Root");
            File.WriteAllText(subFile, "SubFolderFile");

            var entry = new CleanerEntry
            {
                Name = "Test Recurse",
                FileKeys = { FileKeyEntry.Parse($@"{tempFolder}|*.tmp|RECURSE") }
            };

            var scanResult = await _service.AnalyzeAsync(entry);

            Assert.Equal(2, scanResult.FilesToDelete.Count);
            Assert.Contains(rootFile, scanResult.FilesToDelete);
            Assert.Contains(subFile, scanResult.FilesToDelete);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, recursive: true);
        }
    }

    [Fact]
    public async Task CleanAsync_DeletesAnalyzedFilesAndReturnsStats()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "FC_CleaningServiceClean_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var file1 = Path.Combine(tempFolder, "delete1.tmp");
            var file2 = Path.Combine(tempFolder, "delete2.tmp");
            File.WriteAllText(file1, "Delete Me 1");
            File.WriteAllText(file2, "Delete Me 2");

            var entry = new CleanerEntry
            {
                Name = "Clean Test",
                FileKeys = { FileKeyEntry.Parse($@"{tempFolder}|*.tmp") }
            };

            var scanResult = await _service.AnalyzeAsync(entry);
            var (count, bytes) = await _service.CleanAsync(scanResult);

            Assert.Equal(2, count);
            Assert.True(bytes > 0);
            Assert.False(File.Exists(file1));
            Assert.False(File.Exists(file2));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, recursive: true);
        }
    }
}
