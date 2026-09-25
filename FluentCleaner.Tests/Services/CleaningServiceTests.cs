using FluentCleaner.Models;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CleaningServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_CalculatesTotalBytesAndFindsFilesCorrectly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "FCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "test1.tmp");
            var file2 = Path.Combine(tempDir, "test2.tmp");

            byte[] bytes1 = new byte[100];
            byte[] bytes2 = new byte[250];
            await File.WriteAllBytesAsync(file1, bytes1);
            await File.WriteAllBytesAsync(file2, bytes2);

            var service = new CleaningService();
            var entry = new CleanerEntry
            {
                Name = "Test Entry",
                FileKeys = [new FileKeyEntry { Path = tempDir, Pattern = "*.tmp" }]
            };

            // Act
            var result = await service.AnalyzeAsync(entry);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.FilesToDelete.Count);
            Assert.Contains(file1, result.FilesToDelete);
            Assert.Contains(file2, result.FilesToDelete);
            Assert.Equal(350, result.TotalBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
