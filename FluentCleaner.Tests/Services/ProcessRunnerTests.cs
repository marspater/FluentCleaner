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
        var (fileName, arguments) = ProcessRunner.ParseCommandLine(input);

        Assert.Equal(expectedFileName, fileName);
        Assert.Equal(expectedArguments, arguments);
    }
}
