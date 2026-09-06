using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class AppxServiceTests
{
    [Fact]
    public void FilterInstalled_ReturnsEmpty_WhenInstalledIsEmpty()
    {
        var entries = new List<AppxEntry> { new("Weather", "Microsoft.BingWeather", null) };
        var result = AppxService.FilterInstalled(entries, []);
        Assert.Empty(result);
    }

    [Fact]
    public void FilterInstalled_ReturnsEmpty_WhenEntriesIsEmpty()
    {
        var result = AppxService.FilterInstalled([], ["Microsoft.BingWeather"]);
        Assert.Empty(result);
    }

    [Fact]
    public void FilterInstalled_MatchesExactPackageName_CaseInsensitive()
    {
        var entries = new List<AppxEntry>
        {
            new("Bing Weather", "microsoft.bingweather", null),
            new("Bing News", "Microsoft.BingNews", null)
        };
        var installed = new List<string> { "Microsoft.BingWeather" };

        var result = AppxService.FilterInstalled(entries, installed);

        Assert.Single(result);
        Assert.Equal("Bing Weather", result[0].Name);
    }

    [Fact]
    public void FilterInstalled_MatchesPartialPackageName()
    {
        var entries = new List<AppxEntry>
        {
            new("Instagram", "Instagram", null),
            new("Twitter", "Twitter.Twitter", null)
        };
        var installed = new List<string> { "Instagram.Instagram.App_1.0_x64" };

        var result = AppxService.FilterInstalled(entries, installed);

        Assert.Single(result);
        Assert.Equal("Instagram", result[0].Name);
    }

    [Fact]
    public void FilterInstalled_HandlesEmptyPackageName()
    {
        var entries = new List<AppxEntry>
        {
            new("EmptyPkg", "", null),
            new("ValidPkg", "Microsoft.BingWeather", null)
        };
        var installed = new List<string> { "Microsoft.BingWeather" };

        var result = AppxService.FilterInstalled(entries, installed);

        Assert.Equal(2, result.Count);
        Assert.Equal("EmptyPkg", result[0].Name);
        Assert.Equal("ValidPkg", result[1].Name);
    }

    [Fact]
    public void FilterInstalled_PreservesOriginalEntryOrder()
    {
        var entries = new List<AppxEntry>
        {
            new("App A", "Package.A", null),
            new("App B", "Package.B", null),
            new("App C", "Package.C", null)
        };
        var installed = new List<string> { "Package.C.Full", "Package.A" };

        var result = AppxService.FilterInstalled(entries, installed);

        Assert.Equal(2, result.Count);
        Assert.Equal("App A", result[0].Name);
        Assert.Equal("App C", result[1].Name);
    }

    [Fact]
    public async Task ParseDatabaseAsync_ParsesValidWinappxIni()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = """
                          ; Winappx.ini test file
                          [Cortana *]
                          Category=Bloatware
                          Default=False
                          Warning=Removing Cortana disables search integration.
                          PackageName=Microsoft.549981C3F5F10

                          [Bing Weather *]
                          Category=Bloatware
                          Default=False
                          PackageName=Microsoft.BingWeather
                          """;
            await File.WriteAllTextAsync(tempFile, content);

            var entries = await AppxService.ParseDatabaseAsync(tempFile);

            Assert.Equal(2, entries.Count);
            Assert.Equal("Cortana", entries[0].Name);
            Assert.Equal("Microsoft.549981C3F5F10", entries[0].PackageName);
            Assert.Equal("Removing Cortana disables search integration.", entries[0].Warning);

            Assert.Equal("Bing Weather", entries[1].Name);
            Assert.Equal("Microsoft.BingWeather", entries[1].PackageName);
            Assert.Null(entries[1].Warning);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
