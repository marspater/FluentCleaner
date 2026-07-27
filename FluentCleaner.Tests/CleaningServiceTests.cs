using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentCleaner.Models;
using FluentCleaner.Services;
using Moq;
using Xunit;

namespace FluentCleaner.Tests;

public class CleaningServiceTests
{
    private readonly Mock<IFileSystemProvider> _fsMock;
    private readonly CleaningService _service;

    public CleaningServiceTests()
    {
        _fsMock = new Mock<IFileSystemProvider>();

        _service = new CleaningService(_fsMock.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldIdentifyFilesToDelete_WhenNotLockedOrExcluded()
    {
        // Arrange
        var tempPath = Path.GetTempPath().TrimEnd('\\', '/');
        var testPath = Path.Combine(tempPath, "TestApp");
        var testFile = Path.Combine(testPath, "test.log");

        var entry = new CleanerEntry
        {
            Name = "Test App",
            FileKeys = new List<FileKeyEntry>
            {
                new FileKeyEntry { Path = testPath, Pattern = "*.log" }
            }
        };

        _fsMock.Setup(fs => fs.DirectoryExists(testPath)).Returns(true);
        _fsMock.Setup(fs => fs.EnumerateFiles(testPath, "*.log")).Returns(new[] { testFile });
        _fsMock.Setup(fs => fs.TryGetDeletableSize(testFile)).Returns(1024); // Not locked

        // Act
        var result = await _service.AnalyzeAsync(entry);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.FilesToDelete);
        Assert.Contains(testFile, result.FilesToDelete);
        Assert.Equal(1024, result.TotalBytes);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldSkipLockedFiles()
    {
        // Arrange
        var tempPath = Path.GetTempPath().TrimEnd('\\', '/');
        var testPath = Path.Combine(tempPath, "TestApp");
        var testFile = Path.Combine(testPath, "test.log");

        var entry = new CleanerEntry
        {
            Name = "Test App",
            FileKeys = new List<FileKeyEntry>
            {
                new FileKeyEntry { Path = testPath, Pattern = "*.log" }
            }
        };

        _fsMock.Setup(fs => fs.DirectoryExists(testPath)).Returns(true);
        _fsMock.Setup(fs => fs.EnumerateFiles(testPath, "*.log")).Returns(new[] { testFile });
        _fsMock.Setup(fs => fs.TryGetDeletableSize(testFile)).Returns(-1); // Locked

        // Act
        var result = await _service.AnalyzeAsync(entry);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.FilesToDelete);
        Assert.Equal(0, result.TotalBytes);
    }


    [Fact]
    public async Task CleanAsync_ShouldDeleteFiles_WhenCalledWithValidResult()
    {
        // Arrange
        var tempPath = Path.GetTempPath().TrimEnd('\\', '/');
        var testPath = Path.Combine(tempPath, "TestApp");
        var testFile = Path.Combine(testPath, "test.log");

        var entry = new CleanerEntry
        {
            Name = "Test App"
        };

        var result = new ScanResult
        {
            Entry = entry,
            FilesToDelete = new List<string> { testFile }
        };

        _fsMock.Setup(fs => fs.GetFileLength(testFile)).Returns(1024);
        _fsMock.Setup(fs => fs.DeleteFile(testFile)).Verifiable();

        // Act
        var cleanResult = await _service.CleanAsync(result);

        // Assert
        Assert.Equal(1, cleanResult.count);
        Assert.Equal(1024, cleanResult.bytes);
        _fsMock.Verify();
    }

}
