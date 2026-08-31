using FluentCleaner.Models;
using FluentCleaner.Services;
using Xunit;

namespace FluentCleaner.Tests.Services;

public class CategoryResolverTests
{
    [Theory]
    [InlineData(3006, "Microsoft Edge", 5)]
    [InlineData(3021, "Applications", 10)]
    [InlineData(3022, "Internet", 20)]
    [InlineData(3023, "Multimedia", 30)]
    [InlineData(3024, "Utilities", 40)]
    [InlineData(3025, "Windows", 50)]
    [InlineData(3026, "Firefox", 60)]
    [InlineData(3027, "Opera", 70)]
    [InlineData(3028, "Safari", 80)]
    [InlineData(3029, "Google Chrome", 90)]
    [InlineData(3030, "Thunderbird", 100)]
    [InlineData(3031, "Microsoft Store", 110)]
    [InlineData(3033, "Vivaldi", 130)]
    [InlineData(3034, "Brave", 140)]
    [InlineData(3035, "Opera GX", 150)]
    [InlineData(3036, "Spotify", 160)]
    [InlineData(3037, "Avast Secure Browser", 170)]
    [InlineData(3038, "AVG Secure Browser", 180)]
    [InlineData(3039, "Arc Browser", 190)]
    [InlineData(3040, "iTunes", 200)]
    [InlineData(3042, "WhatsApp", 210)]
    [InlineData(3043, "Norton Private Browser", 220)]
    [InlineData(3044, "Avira Secure Browser", 230)]
    public void TryMapLangSecRef_ReturnsMappedCategory_WhenLangSecRefIsKnown(int code, string expectedName, int expectedOrder)
    {
        var entry = new CleanerEntry
        {
            LangSecRef = code,
            Section = "Overridden Section"
        };

        var category = CategoryResolver.TryMapLangSecRef(entry);

        Assert.Equal(expectedName, category.Name);
        Assert.Equal(expectedOrder, category.Order);
    }

    [Theory]
    [InlineData(null, "Custom Section")]
    [InlineData(9999, "Unknown Code Section")]
    [InlineData(3032, "CCleaner Browser Section")]
    public void TryMapLangSecRef_ReturnsSectionCategory_WhenLangSecRefUnmappedAndSectionPresent(int? code, string section)
    {
        var entry = new CleanerEntry
        {
            LangSecRef = code,
            Section = section
        };

        var category = CategoryResolver.TryMapLangSecRef(entry);

        Assert.Equal(section, category.Name);
        Assert.Equal(1000, category.Order);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData(null, "   ")]
    [InlineData(9999, null)]
    [InlineData(9999, "")]
    [InlineData(9999, "   ")]
    public void TryMapLangSecRef_ReturnsOtherApplications_WhenLangSecRefUnmappedAndSectionNullOrWhitespace(int? code, string? section)
    {
        var entry = new CleanerEntry
        {
            LangSecRef = code,
            Section = section
        };

        var category = CategoryResolver.TryMapLangSecRef(entry);

        Assert.Equal("Other Applications", category.Name);
        Assert.Equal(2000, category.Order);
    }
}
