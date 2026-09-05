using System;
using System.IO;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class PathExpanderTests
{
    private readonly PathExpander _expander = new();

    [Fact]
    public void ExpandVariables_ReturnsUnchanged_WhenNoVariablesPresent()
    {
        var path = @"C:\Folder\SubFolder\file.txt";
        var result = _expander.ExpandVariables(path);
        Assert.Equal(path, result);
    }

    [Theory]
    [InlineData("%AppData%", Environment.SpecialFolder.ApplicationData)]
    [InlineData("%LocalAppData%", Environment.SpecialFolder.LocalApplicationData)]
    [InlineData("%UserProfile%", Environment.SpecialFolder.UserProfile)]
    [InlineData("%ProgramFiles%", Environment.SpecialFolder.ProgramFiles)]
    [InlineData("%ProgramFiles(x86)%", Environment.SpecialFolder.ProgramFilesX86)]
    [InlineData("%ProgramFilesX86%", Environment.SpecialFolder.ProgramFilesX86)]
    [InlineData("%ProgramData%", Environment.SpecialFolder.CommonApplicationData)]
    [InlineData("%CommonAppData%", Environment.SpecialFolder.CommonApplicationData)]
    [InlineData("%Documents%", Environment.SpecialFolder.MyDocuments)]
    [InlineData("%Desktop%", Environment.SpecialFolder.DesktopDirectory)]
    [InlineData("%Music%", Environment.SpecialFolder.MyMusic)]
    [InlineData("%Pictures%", Environment.SpecialFolder.MyPictures)]
    [InlineData("%Videos%", Environment.SpecialFolder.MyVideos)]
    [InlineData("%SystemRoot%", Environment.SpecialFolder.Windows)]
    [InlineData("%WinDir%", Environment.SpecialFolder.Windows)]
    [InlineData("%System%", Environment.SpecialFolder.System)]
    [InlineData("%SystemX86%", Environment.SpecialFolder.SystemX86)]
    public void ExpandVariables_ExpandsKnownSpecialFolders(string token, Environment.SpecialFolder folder)
    {
        var expectedBase = Environment.GetFolderPath(folder).TrimEnd('\\', '/');
        var inputPath = $@"{token}\TestFolder";
        var result = _expander.ExpandVariables(inputPath);

        if (!string.IsNullOrEmpty(expectedBase))
        {
            Assert.Equal($@"{expectedBase}\TestFolder", result);
        }
    }

    [Fact]
    public void ExpandVariables_IsCaseInsensitiveForKnownVars()
    {
        var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(expectedBase)) return;

        var lowerResult = _expander.ExpandVariables(@"%appdata%\SubFolder");
        var upperResult = _expander.ExpandVariables(@"%APPDATA%\SubFolder");
        var mixedResult = _expander.ExpandVariables(@"%aPpDaTa%\SubFolder");

        Assert.Equal($@"{expectedBase}\SubFolder", lowerResult);
        Assert.Equal($@"{expectedBase}\SubFolder", upperResult);
        Assert.Equal($@"{expectedBase}\SubFolder", mixedResult);
    }

    [Fact]
    public void ExpandVariables_ExpandsLocalLowAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData)) return;

        var expectedLocalLow = Path.Combine(localAppData, "..", "LocalLow").TrimEnd('\\', '/');
        var result = _expander.ExpandVariables(@"%LocalLowAppData%\Vendor\App");

        Assert.Equal($@"{expectedLocalLow}\Vendor\App", result);
    }

    [Fact]
    public void ExpandVariables_ExpandsTempAndTmp()
    {
        var expectedTemp = Path.GetTempPath().TrimEnd('\\', '/');
        var tempResult = _expander.ExpandVariables(@"%Temp%\Cache");
        var tmpResult = _expander.ExpandVariables(@"%Tmp%\Cache");

        Assert.Equal($@"{expectedTemp}\Cache", tempResult);
        Assert.Equal($@"{expectedTemp}\Cache", tmpResult);
    }

    [Fact]
    public void ExpandVariables_ExpandsSystemDrive_InCompoundPath()
    {
        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var expectedDrive = Path.GetPathRoot(windowsFolder)?.TrimEnd('\\', '/') ?? "C:";
        var result = _expander.ExpandVariables(@"%SystemDrive%\Windows\System32");

        Assert.Equal($@"{expectedDrive}\Windows\System32", result);
    }

    [Fact]
    public void ExpandVariables_AppendsDirectorySeparator_ForBareDriveLetter()
    {
        var result = _expander.ExpandVariables("C:");
        Assert.Equal($"C:{Path.DirectorySeparatorChar}", result);
    }

    [Fact]
    public void ExpandVariables_FallsBackToOSEnvironmentVariables()
    {
        var varName = "FLUENTCLEANER_TEST_EXPAND_VAR";
        var varValue = "CustomTestValue";
        Environment.SetEnvironmentVariable(varName, varValue);

        try
        {
            var result = _expander.ExpandVariables($"%{varName}%\\SubDir");
            Assert.Equal($@"{varValue}\SubDir", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    public void ExpandVariables_ExpandsMultipleTokensInSinglePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd('\\', '/');
        var temp = Path.GetTempPath().TrimEnd('\\', '/');

        if (string.IsNullOrEmpty(appData)) return;

        var result = _expander.ExpandVariables(@"%AppData%|%Temp%");
        Assert.Equal($"{appData}|{temp}", result);
    }
}
