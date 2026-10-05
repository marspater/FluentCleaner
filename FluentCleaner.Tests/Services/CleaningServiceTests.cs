using System;
using System.IO;
using System.Threading.Tasks;
using FluentCleaner.Models;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CleaningServiceTests : IDisposable
{
    private readonly string _tempDir;

    public CleaningServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FluentCleaner_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldDetectDeletableFilesAndSize()
    {
        // Arrange
        var subDir = Path.Combine(_tempDir, "Logs");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(_tempDir, "test1.tmp");
        var file2 = Path.Combine(subDir, "test2.log");

        File.WriteAllText(file1, "Hello World!"); // 12 bytes
        File.WriteAllText(file2, "Testing CleaningService"); // 23 bytes

        var entry = new CleanerEntry
        {
            Name = "Test Application",
            FileKeys =
            {
                new FileKeyEntry { Path = _tempDir, Pattern = "*.tmp", Flag = FileKeyFlag.None },
                new FileKeyEntry { Path = subDir, Pattern = "*.log", Flag = FileKeyFlag.Recurse }
            }
        };

        var service = new CleaningService();

        // Act
        var result = await service.AnalyzeAsync(entry);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(file1, result.FilesToDelete);
        Assert.Contains(file2, result.FilesToDelete);
        Assert.Equal(12 + 23, result.TotalBytes);
    }

    [Fact]
    public async Task CleanAsync_ShouldDeleteFilesAndReturnCountAndBytes()
    {
        // Arrange
        var file1 = Path.Combine(_tempDir, "clean1.tmp");
        File.WriteAllText(file1, "1234567890"); // 10 bytes

        var entry = new CleanerEntry
        {
            Name = "Clean Application",
            FileKeys = { new FileKeyEntry { Path = _tempDir, Pattern = "*.tmp", Flag = FileKeyFlag.None } }
        };

        var service = new CleaningService();
        var scanResult = await service.AnalyzeAsync(entry);

        // Act
        var (count, bytes) = await service.CleanAsync(scanResult);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(10, bytes);
        Assert.False(File.Exists(file1));
    }
}
