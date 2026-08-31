using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CommandLineParserTests
{
    [Theory]
    [InlineData("cleanmgr.exe /sagerun:1", "cleanmgr.exe", "/sagerun:1")]
    [InlineData("\"C:\\Program Files\\Cleaner\\clean.exe\" --silent -v", "C:\\Program Files\\Cleaner\\clean.exe", "--silent -v")]
    [InlineData("notepad.exe", "notepad.exe", "")]
    [InlineData("  \"C:\\Tool.exe\"  args  ", "C:\\Tool.exe", "args")]
    [InlineData("", "", "")]
    [InlineData("   ", "", "")]
    public void Parse_ReturnsExpectedFileNameAndArguments(string input, string expectedFileName, string expectedArguments)
    {
        var (fileName, arguments) = CommandLineParser.Parse(input);

        Assert.Equal(expectedFileName, fileName);
        Assert.Equal(expectedArguments, arguments);
    }
}
