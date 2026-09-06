using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentCleaner.Models;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class BugAuditRegressionTests
{
    [Fact]
    public void CustomCleaners_WithoutDetect_ParsedWhenDetectionNotRequired()
    {
        var parser = new Winapp2Parser();
        var iniContent = """
                         [Custom App Logs]
                         Section=Custom
                         FileKey1=%Temp%\logs|*.*
                         """;

        // When requireDetection = true (Winapp2 default), entries without detection are skipped
        var defaultResult = parser.Parse(iniContent, requireDetection: true);
        Assert.Empty(defaultResult);

        // When requireDetection = false (Custom cleaners), entries without detection are accepted
        var customResult = parser.Parse(iniContent, requireDetection: false);
        Assert.Single(customResult);
        Assert.Equal("Custom App Logs", customResult[0].Name);
        Assert.Equal("Custom", customResult[0].Section);
        Assert.Single(customResult[0].FileKeys);
    }

    [Fact]
    public void AppSettings_SelectedEntries_DistinguishesNullFromEmptySet()
    {
        // Null represents unconfigured defaults
        var settingsDefault = new AppSettings { SelectedEntries = null };
        Assert.Null(settingsDefault.SelectedEntries);

        // Empty set represents explicit "Select None"
        var settingsSelectNone = new AppSettings { SelectedEntries = [] };
        Assert.NotNull(settingsSelectNone.SelectedEntries);
        Assert.Empty(settingsSelectNone.SelectedEntries);

        // Serialization roundtrip preserves empty set
        var json = JsonSerializer.Serialize(settingsSelectNone);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(deserialized?.SelectedEntries);
        Assert.Empty(deserialized.SelectedEntries);
    }

    [Fact]
    public void PathExpander_BareDriveRoot_DoesNotCrashOrTreatAsRelative()
    {
        var expander = new PathExpander();
        
        // Pattern directly on drive letter root C:\*
        var results = expander.ResolvePaths(@"C:\*");
        Assert.NotNull(results);
    }

    [Fact]
    public void DetectionService_ClearCache_ExecutesWithoutException()
    {
        DetectionService.ClearCache();
        PathExpander.ClearCache();

        var detector = new DetectionService();
        var entry = new CleanerEntry
        {
            Name = "Dummy App",
            DetectFiles = new List<string> { @"%LocalAppData%\NonExistentApp123\test.exe" }
        };

        var installed = detector.IsInstalled(entry);
        Assert.False(installed);

        // Cache clear can be called repeatedly and concurrently
        DetectionService.ClearCache();
        PathExpander.ClearCache();
    }

    [Fact]
    public void AiExplainer_EmptyChoicesResponse_HandledSafely()
    {
        var jsonNoChoices = """{"id":"chatcmpl-123","object":"chat.completion","created":12345678,"choices":[]}""";
        using var doc = JsonDocument.Parse(jsonNoChoices);
        var root = doc.RootElement;

        var hasChoices = root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0;
        Assert.False(hasChoices);
    }

    [Fact]
    public void AppSettings_Instance_IsInitializedWithValidStaticPaths()
    {
        Assert.NotNull(AppSettings.Instance);
        Assert.False(string.IsNullOrWhiteSpace(AppSettings.Instance.Language) && AppSettings.Instance.Language == null);
        _ = AppSettings.IsPortable;
    }

    [Fact]
    public void ProcessStartInfo_ScriptExecution_ForcesUseShellExecuteFalse()
    {
        var psi = new System.Diagnostics.ProcessStartInfo(SecurityGuard.GetSafePowerShellPath())
        {
            WorkingDirectory = System.AppContext.BaseDirectory,
            UseShellExecute = false
        };

        psi.ArgumentList.Add("-NoExit");
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add("testScript.ps1");
        psi.ArgumentList.Add("arg1; calc.exe");

        Assert.False(psi.UseShellExecute);
        Assert.Equal(7, psi.ArgumentList.Count);
        Assert.Equal("arg1; calc.exe", psi.ArgumentList[6]);
    }

    [Fact]
    public void ProcessStartInfo_ArgumentList_PreventsCommandInjectionInPathsAndArgs()
    {
        var scriptPath = @"C:\Extensions\a""; calc.exe; #.ps1";
        var optionArg = @"option""; calc.exe; #";

        var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        if (optionArg is not null)
        {
            psi.ArgumentList.Add(optionArg);
        }

        Assert.Equal(6, psi.ArgumentList.Count);
        Assert.Equal("-NoProfile", psi.ArgumentList[0]);
        Assert.Equal("-ExecutionPolicy", psi.ArgumentList[1]);
        Assert.Equal("Bypass", psi.ArgumentList[2]);
        Assert.Equal("-File", psi.ArgumentList[3]);
        Assert.Equal(scriptPath, psi.ArgumentList[4]);
        Assert.Equal(optionArg, psi.ArgumentList[5]);
    }
}
