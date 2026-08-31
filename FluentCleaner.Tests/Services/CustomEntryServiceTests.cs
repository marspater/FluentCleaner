using System;
using System.IO;
using System.Threading.Tasks;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CustomEntryServiceTests : IDisposable
{
    private readonly CustomEntryService _service = new();
    private readonly string _customDir = CustomEntryService.CustomDir;

    public CustomEntryServiceTests()
    {
        CleanupCustomDir();
    }

    public void Dispose()
    {
        CleanupCustomDir();
    }

    private void CleanupCustomDir()
    {
        if (Directory.Exists(_customDir))
        {
            try
            {
                Directory.Delete(_customDir, true);
            }
            catch
            {
                // Ignore transient cleanup errors
            }
        }
    }

    [Fact]
    public async Task LoadEnabledEntriesAsync_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        CleanupCustomDir();

        var result = await _service.LoadEnabledEntriesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadEnabledEntriesAsync_ReturnsEmpty_WhenDirectoryIsEmpty()
    {
        Directory.CreateDirectory(_customDir);

        var result = await _service.LoadEnabledEntriesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadEnabledEntriesAsync_IgnoresDisabledFiles()
    {
        Directory.CreateDirectory(_customDir);

        var tempPath = Path.GetTempPath();

        // Active ini file
        var activePath = Path.Combine(_customDir, "Active.ini");
        var activeIni = $"""
                        [Active App]
                        DetectFile={tempPath}
                        FileKey1=%Temp%|*.*
                        """;
        await File.WriteAllTextAsync(activePath, activeIni);

        // Disabled ini file (.ini.disabled)
        var disabledPath = Path.Combine(_customDir, "Disabled.ini.disabled");
        var disabledIni = $"""
                          [Disabled App]
                          DetectFile={tempPath}
                          FileKey1=%Temp%|*.*
                          """;
        await File.WriteAllTextAsync(disabledPath, disabledIni);

        var result = await _service.LoadEnabledEntriesAsync();

        Assert.Single(result);
        Assert.Equal("Active App", result[0].Name);
        Assert.True(result[0].IsCustom);
    }

    [Fact]
    public async Task LoadEnabledEntriesAsync_FiltersOutEntriesWithFailedDetection()
    {
        Directory.CreateDirectory(_customDir);

        // Non-existent path for detection file
        var nonExistentPath = Path.Combine(_customDir, "NonExistentFolder_12345");
        var iniPath = Path.Combine(_customDir, "Detected.ini");
        var iniContent = $"""
                         [Undetected App]
                         DetectFile={nonExistentPath}
                         FileKey1=%Temp%|*.*
                         """;
        await File.WriteAllTextAsync(iniPath, iniContent);

        var result = await _service.LoadEnabledEntriesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadEnabledEntriesAsync_IncludesEntriesWhenDetectionSucceeds()
    {
        Directory.CreateDirectory(_customDir);

        // Create a real directory to detect
        var detectFolder = Path.Combine(_customDir, "ExistingDetectFolder");
        Directory.CreateDirectory(detectFolder);

        // Entry with passing detection
        var iniPath1 = Path.Combine(_customDir, "PassedDetect.ini");
        var iniContent1 = $"""
                          [Passed App]
                          DetectFile={detectFolder}
                          FileKey1=%Temp%|*.*
                          """;
        await File.WriteAllTextAsync(iniPath1, iniContent1);

        var result = await _service.LoadEnabledEntriesAsync();

        Assert.Single(result);
        Assert.Equal("Passed App", result[0].Name);
        Assert.True(result[0].IsCustom);
    }

    [Fact]
    public async Task LoadEnabledEntriesAsync_DeduplicatesEntriesByName()
    {
        Directory.CreateDirectory(_customDir);

        var tempPath = Path.GetTempPath();

        var iniPath1 = Path.Combine(_customDir, "Custom1.ini");
        var iniContent1 = $"""
                          [Duplicate App]
                          DetectFile={tempPath}
                          FileKey1=%Temp%|file1.tmp
                          """;
        await File.WriteAllTextAsync(iniPath1, iniContent1);

        var iniPath2 = Path.Combine(_customDir, "Custom2.ini");
        var iniContent2 = $"""
                          [DUPLICATE APP]
                          DetectFile={tempPath}
                          FileKey1=%Temp%|file2.tmp
                          """;
        await File.WriteAllTextAsync(iniPath2, iniContent2);

        var result = await _service.LoadEnabledEntriesAsync();

        Assert.Single(result);
        Assert.Equal("DUPLICATE APP", result[0].Name, ignoreCase: true);
        Assert.Equal("file2.tmp", result[0].FileKeys[0].Pattern);
    }
}
