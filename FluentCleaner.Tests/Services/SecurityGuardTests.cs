using System;
using System.IO;
using Xunit;
using FluentCleaner.Services;

namespace FluentCleaner.Tests.Services;

public class SecurityGuardTests
{
    [Theory]
    [InlineData("C:\\")]
    [InlineData("C:")]
    [InlineData("D:\\")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsSafeDeletionPath_RejectsDriveRootsAndEmpty(string? path)
    {
        Assert.False(SecurityGuard.IsSafeDeletionPath(path));
    }

    [Fact]
    public void IsSafeDeletionPath_RejectsProtectedSystemDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.False(SecurityGuard.IsSafeDeletionPath(winDir));
        Assert.False(SecurityGuard.IsSafeDeletionPath(sysDir));
        Assert.False(SecurityGuard.IsSafeDeletionPath(progFiles));
        Assert.False(SecurityGuard.IsSafeDeletionPath(userProfile));
    }

    [Fact]
    public void IsSafeDeletionPath_AllowsSafeApplicationSubdirectories()
    {
        var tempDir = Path.GetTempPath();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var safeSub = Path.Combine(localAppData, "MyApp", "Cache");

        Assert.True(SecurityGuard.IsSafeDeletionPath(tempDir));
        Assert.True(SecurityGuard.IsSafeDeletionPath(safeSub));
    }

    [Theory]
    [InlineData("HKLM", "Software", null)]
    [InlineData("HKCU", "Software", null)]
    [InlineData("HKLM", "System", null)]
    [InlineData("HKCU", "Software\\Microsoft", null)]
    [InlineData("HKLM", "", null)]
    [InlineData("HKCU", null, null)]
    public void IsSafeRegistryDeletion_RejectsCriticalOrShallowTreeDeletions(string hive, string? subKey, string? valueName)
    {
        Assert.False(SecurityGuard.IsSafeRegistryDeletion(hive, subKey!, valueName));
    }

    [Theory]
    [InlineData("HKCU", "Software\\MyCompany\\MyApp", null)]
    [InlineData("HKLM", "Software\\MyCompany\\MyApp\\Cache", null)]
    [InlineData("HKCU", "Software\\Microsoft\\Windows\\CurrentVersion\\Run", "SomeApp")]
    public void IsSafeRegistryDeletion_AllowsDeepSubkeysOrNamedValues(string hive, string subKey, string? valueName)
    {
        Assert.True(SecurityGuard.IsSafeRegistryDeletion(hive, subKey, valueName));
    }

    [Theory]
    [InlineData("../../evil.ini", "evil.ini")]
    [InlineData("..\\..\\evil", "evil")]
    [InlineData("CON", "_CON")]
    [InlineData("PRN", "_PRN")]
    [InlineData("AUX.ini", "_AUX.ini")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1", "_COM1")]
    [InlineData("valid_name", "valid_name")]
    [InlineData("My Cleaner 2026", "My Cleaner 2026")]
    public void SanitizeFileName_PreventsTraversalAndReservedNames(string input, string expected)
    {
        var sanitized = SecurityGuard.SanitizeFileName(input);
        Assert.Equal(expected, sanitized);
    }

    [Theory]
    [InlineData("Microsoft.BingNews", true)]
    [InlineData("king.com.CandyCrushSaga", true)]
    [InlineData("*Spotify*", true)]
    [InlineData("Microsoft.549981C3F5F10", true)]
    [InlineData("foo'; Remove-Item C:\\; #", false)]
    [InlineData("$(whoami)", false)]
    [InlineData("test | dir", false)]
    [InlineData("test & calc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPackageName_ValidatesCorrectly(string? name, bool expected)
    {
        Assert.Equal(expected, SecurityGuard.IsValidPackageName(name));
    }

    [Theory]
    [InlineData("https://github.com/marspater/FluentCleaner", true)]
    [InlineData("http://example.com/test", true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("cmd.exe /c calc", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ms-appinstaller:?source=https://evil.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidWebUrl_EnforcesHttpOrHttps(string? url, bool expected)
    {
        Assert.Equal(expected, SecurityGuard.IsValidWebUrl(url));
    }
}
