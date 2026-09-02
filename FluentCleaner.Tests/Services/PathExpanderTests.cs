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
        var input = @"C:\Windows\System32\drivers\etc\hosts";
        var result = _expander.ExpandVariables(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ExpandVariables_ExpandsAppData()
    {
        var input = @"%AppData%\TestApp";
        var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd('\\', '/');
        var expected = Path.Combine(expectedBase, "TestApp");

        var result = _expander.ExpandVariables(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandVariables_ExpandsLocalAppData()
    {
        var input = @"%LocalAppData%\Google\Chrome";
        var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).TrimEnd('\\', '/');
        var expected = Path.Combine(expectedBase, @"Google\Chrome");

        var result = _expander.ExpandVariables(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandVariables_AppendsDirectorySeparator_ForBareDriveRoot()
    {
        var input = "%SystemDrive%";
        var result = _expander.ExpandVariables(input);

        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
        Assert.True(result.Length == 3 && char.IsLetter(result[0]) && result[1] == ':');
    }

    [Fact]
    public void ExpandVariables_FallsBackToEnvironment_ForUnknownVariables()
    {
        Environment.SetEnvironmentVariable("FLUENT_CLEANER_TEST_VAR", @"C:\TestFolder");
        try
        {
            var input = @"%FLUENT_CLEANER_TEST_VAR%\subfolder";
            var result = _expander.ExpandVariables(input);
            Assert.Equal(@"C:\TestFolder\subfolder", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLUENT_CLEANER_TEST_VAR", null);
        }
    }

    [Fact]
    public void ResolvePaths_ResolvesProgramFilesX86_WhenProgramFilesPresent()
    {
        var input = @"%ProgramFiles%\NonExistentTestDir12345";
        var results = _expander.ResolvePaths(input);

        Assert.NotEmpty(results);
    }
}
