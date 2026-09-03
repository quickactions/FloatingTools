using System.Globalization;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class ApplicationLanguageResolverTests
{
    [Theory]
    [InlineData(ApplicationLanguageMode.English, "he-IL", ApplicationDisplayLanguage.English)]
    [InlineData(ApplicationLanguageMode.Hebrew, "en-US", ApplicationDisplayLanguage.Hebrew)]
    [InlineData(ApplicationLanguageMode.System, "he-IL", ApplicationDisplayLanguage.Hebrew)]
    [InlineData(ApplicationLanguageMode.System, "en-US", ApplicationDisplayLanguage.English)]
    [InlineData(ApplicationLanguageMode.System, "ar-IL", ApplicationDisplayLanguage.English)]
    [InlineData((ApplicationLanguageMode)999, "he", ApplicationDisplayLanguage.Hebrew)]
    public void Resolve_UsesExplicitPreferenceOrSystemUiCulture(
        ApplicationLanguageMode mode,
        string culture,
        ApplicationDisplayLanguage expected)
    {
        var resolver = new ApplicationLanguageResolver(
            () => new CultureInfo(culture));

        Assert.Equal(expected, resolver.Resolve(mode));
    }
}
