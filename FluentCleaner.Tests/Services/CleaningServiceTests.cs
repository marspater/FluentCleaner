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
    public void FileKeyEntry_Pattern_SplitsAndCachesPatterns()
    {
        var entry = new FileKeyEntry { Pattern = " *.tmp ; *.log ; *.bak " };

        Assert.Equal("*.tmp ; *.log ; *.bak", entry.Pattern);
        Assert.Equal(3, entry.Patterns.Length);
        Assert.Equal("*.tmp", entry.Patterns[0]);
        Assert.Equal("*.log", entry.Patterns[1]);
        Assert.Equal("*.bak", entry.Patterns[2]);
    }

    [Fact]
    public void FileKeyEntry_Pattern_DefaultValue_CachesDefaultPattern()
    {
        var entry = new FileKeyEntry();

        Assert.Equal("*.*", entry.Pattern);
        Assert.Single(entry.Patterns);
        Assert.Equal("*.*", entry.Patterns[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_FindsMatchingFiles_AndRespectsExclusions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "test1.tmp");
            var file2 = Path.Combine(tempDir, "test2.log");
            var file3 = Path.Combine(tempDir, "ignore.txt");
            var file4 = Path.Combine(tempDir, "excluded.tmp");

            await File.WriteAllTextAsync(file1, "hello"); // 5 bytes
            await File.WriteAllTextAsync(file2, "world!!!"); // 8 bytes
            await File.WriteAllTextAsync(file3, "skip me");
            await File.WriteAllTextAsync(file4, "excluded content");

            var cleanerEntry = new CleanerEntry
            {
                Name = "Test App",
                FileKeys =
                [
                    new FileKeyEntry
                    {
                        Path = tempDir,
                        Pattern = "*.tmp;*.log"
                    }
                ],
                ExcludeKeys =
                [
                    new ExcludeKeyEntry
                    {
                        Type = ExcludeType.File,
                        Path = tempDir,
                        Pattern = "excluded.tmp"
                    }
                ]
            };

            var cleaningService = new CleaningService();
            var scanResult = await cleaningService.AnalyzeAsync(cleanerEntry);

            Assert.Equal(2, scanResult.FilesToDelete.Count);
            Assert.Contains(file1, scanResult.FilesToDelete);
            Assert.Contains(file2, scanResult.FilesToDelete);
            Assert.DoesNotContain(file3, scanResult.FilesToDelete);
            Assert.DoesNotContain(file4, scanResult.FilesToDelete);
            Assert.Equal(13, scanResult.TotalBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); }
                catch { }
            }
        }
    }
}
