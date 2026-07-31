using System.Linq;
using FluentCleaner.Models;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class Winapp2ParserTests
{
    private readonly Winapp2Parser _parser = new();

    [Fact]
    public void Parse_ReturnsEmpty_WhenContentIsEmpty()
    {
        var result = _parser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_IgnoresComments()
    {
        var content = """
                      ; This is a comment
                      # This is also a comment
                      [Test App]
                      Detect=HKCU\Software\Test
                      FileKey1=%AppData%\Test|*.*
                      """;
        var result = _parser.Parse(content);
        Assert.Single(result);
        Assert.Equal("Test App", result[0].Name);
    }

    [Fact]
    public void Parse_IgnoresWinapp2Header()
    {
        var content = """
                      [Winapp2]
                      somekey=value
                      [version]
                      somekey=value
                      [Valid App]
                      Detect=HKCU\Software\Test
                      FileKey1=%AppData%\Test|*.*
                      """;
        var result = _parser.Parse(content);
        Assert.Single(result);
        Assert.Equal("Valid App", result[0].Name);
    }

    [Fact]
    public void Parse_ParsesBasicProperties()
    {
        var content = """
                      [Test App]
                      LangSecRef=3025
                      Section=Windows
                      SpecialDetect=DET_TEST
                      Warning=Will remove history
                      Default=False
                      FileKey1=%AppData%\Test|*.*
                      """;
        var result = _parser.Parse(content);
        Assert.Single(result);
        var entry = result[0];
        Assert.Equal("Test App", entry.Name);
        Assert.Equal(3025, entry.LangSecRef);
        Assert.Equal("Windows", entry.Section);
        Assert.Equal("DET_TEST", entry.SpecialDetect);
        Assert.Equal("Will remove history", entry.Warning);
        Assert.False(entry.Default);
    }

    [Fact]
    public void Parse_ParsesDetectionAndActions()
    {
        var content = """
                      [Test App]
                      Detect=HKCU\Software\Test
                      DetectFile=%AppData%\Test
                      FileKey1=%AppData%\Test|*.tmp|RECURSE
                      RegKey1=HKCU\Software\Test|ValueName
                      ExcludeKey1=FILE|%AppData%\Test|*.db
                      """;
        var result = _parser.Parse(content);
        Assert.Single(result);
        var entry = result[0];

        Assert.Single(entry.DetectKeys);
        Assert.Equal(@"HKCU\Software\Test", entry.DetectKeys[0]);

        Assert.Single(entry.DetectFiles);
        Assert.Equal(@"%AppData%\Test", entry.DetectFiles[0]);

        Assert.Single(entry.FileKeys);
        Assert.Equal(@"%AppData%\Test", entry.FileKeys[0].Path);
        Assert.Equal("*.tmp", entry.FileKeys[0].Pattern);
        Assert.Equal(FileKeyFlag.Recurse, entry.FileKeys[0].Flag);

        Assert.Single(entry.RegKeys);
        Assert.Equal(@"HKCU\Software\Test", entry.RegKeys[0].KeyPath);
        Assert.Equal("ValueName", entry.RegKeys[0].ValueName);

        Assert.Single(entry.ExcludeKeys);
        Assert.Equal(ExcludeType.File, entry.ExcludeKeys[0].Type);
        Assert.Equal(@"%AppData%\Test", entry.ExcludeKeys[0].Path);
        Assert.Equal("*.db", entry.ExcludeKeys[0].Pattern);
    }

    [Fact]
    public void Parse_StripsCommunityAsterisk()
    {
        var content = """
                      [Community App *]
                      Detect=HKCU\Software\Test
                      FileKey1=%AppData%\Test|*.*
                      """;
        var result = _parser.Parse(content);
        Assert.Single(result);
        Assert.Equal("Community App", result[0].Name);
    }


    [Theory]
    [InlineData(@"%AppData%\Test|")]
    [InlineData(@"%AppData%\Test||RECURSE")]
    public void Parse_DefaultsEmptyFileKeyPatternToAllFiles(string fileKey)
    {
        var content = $"""
                      [Test App]
                      Detect=HKCU\Software\Test
                      FileKey1={fileKey}
                      """;

        var result = _parser.Parse(content);

        Assert.Single(result);
        Assert.Equal("*.*", result[0].FileKeys[0].Pattern);
    }

    [Fact]
    public void Parse_TreatsEmptyExcludeKeyPatternAsDirectoryExclusion()
    {
        var content = """
                      [Test App]
                      Detect=HKCU\Software\Test
                      FileKey1=%AppData%\Test|*.*
                      ExcludeKey1=PATH|%AppData%\Test\Keep|
                      """;

        var result = _parser.Parse(content);

        Assert.Single(result);
        Assert.Null(result[0].ExcludeKeys[0].Pattern);
    }

    [Fact]
    public void Parse_IgnoresInvalidEntries()
    {
        var content = """
                      [No Detect App]
                      FileKey1=%AppData%\Test|*.*

                      [No Actions App]
                      Detect=HKCU\Software\Test

                      [Valid App]
                      Detect=HKCU\Software\Test
                      FileKey1=%AppData%\Test|*.*
                      """;
        var result = _parser.Parse(content);
        Assert.Single(result);
        Assert.Equal("Valid App", result[0].Name);
    }

    [Fact]
    public void Parse_HandlesDifferentLineEndings()
    {
        var content = "[App1]\nDetect=HKCU\nFileKey1=C:\\|*.*\n[App2]\rDetect=HKCU\rFileKey1=C:\\|*.*\r[App3]\r\nDetect=HKCU\r\nFileKey1=C:\\|*.*";
        var result = _parser.Parse(content);
        Assert.Equal(3, result.Count);
        Assert.Equal("App1", result[0].Name);
        Assert.Equal("App2", result[1].Name);
        Assert.Equal("App3", result[2].Name);
    }
}
