using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class ProcessRunnerTests
{
    [Theory]
    [InlineData("", "", "")]
    [InlineData("   ", "", "")]
    [InlineData("cleaner.exe", "cleaner.exe", "")]
    [InlineData("cleaner.exe /arg1 /arg2", "cleaner.exe", "/arg1 /arg2")]
    [InlineData("\"C:\\Program Files\\My Tool\\tool.exe\"", "C:\\Program Files\\My Tool\\tool.exe", "")]
    [InlineData("\"C:\\Program Files\\My Tool\\tool.exe\" --clean --silent", "C:\\Program Files\\My Tool\\tool.exe", "--clean --silent")]
    [InlineData("notepad.exe \"C:\\My Files\\test.txt\"", "notepad.exe", "\"C:\\My Files\\test.txt\"")]
    public void ParseCommandLine_ParsesExpectedFileNameAndArguments(string input, string expectedFileName, string expectedArguments)
    {
        var (fileName, arguments) = ProcessRunner.ParseCommandLine(input, _ => false);

        Assert.Equal(expectedFileName, fileName);
        Assert.Equal(expectedArguments, arguments);
    }

    [Fact]
    public void ParseCommandLine_UnquotedPath_WhenFileExists_ResolvesLongestMatchingFilePath()
    {
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Program Files\My App\app.exe"
        };

        var (fileName, arguments) = ProcessRunner.ParseCommandLine(
            @"C:\Program Files\My App\app.exe /silent --log",
            file => existingFiles.Contains(file));

        Assert.Equal(@"C:\Program Files\My App\app.exe", fileName);
        Assert.Equal("/silent --log", arguments);
    }

    [Fact]
    public void ParseCommandLine_UnquotedPath_WhenMultipleMatchesExist_ResolvesLongestMatch()
    {
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Program.exe",
            @"C:\Program Files\App.exe"
        };

        var (fileName, arguments) = ProcessRunner.ParseCommandLine(
            @"C:\Program Files\App.exe /run",
            file => existingFiles.Contains(file));

        Assert.Equal(@"C:\Program Files\App.exe", fileName);
        Assert.Equal("/run", arguments);
    }

    [Fact]
    public void ParseCommandLine_UnquotedPath_FallbackExtensionMatch_ResolvesExecutableWithArgs()
    {
        var (fileName, arguments) = ProcessRunner.ParseCommandLine(
            @"C:\Program Files\Custom App\cleaner.exe --autoclean",
            _ => false);

        Assert.Equal(@"C:\Program Files\Custom App\cleaner.exe", fileName);
        Assert.Equal("--autoclean", arguments);
    }
}
