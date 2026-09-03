using System.Globalization;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class CalendarLanguageResolverTests
{
    [Fact]
    public void ExplicitCalendarLanguageOverridesApplicationLanguage()
    {
        var appLanguage = ApplicationLanguageMode.Hebrew;
        var resolver = new CalendarLanguageResolver(
            () => appLanguage,
            new ApplicationLanguageResolver(() => new CultureInfo("he-IL")));

        Assert.Equal(CalendarDisplayLanguage.English,
            resolver.Resolve(CalendarLanguageMode.English));

        appLanguage = ApplicationLanguageMode.English;
        Assert.Equal(CalendarDisplayLanguage.Hebrew,
            resolver.Resolve(CalendarLanguageMode.Hebrew));
    }

    [Theory]
    [InlineData(ApplicationLanguageMode.English, "he-IL", CalendarDisplayLanguage.English)]
    [InlineData(ApplicationLanguageMode.Hebrew, "en-US", CalendarDisplayLanguage.Hebrew)]
    [InlineData(ApplicationLanguageMode.System, "he-IL", CalendarDisplayLanguage.Hebrew)]
    [InlineData(ApplicationLanguageMode.System, "en-US", CalendarDisplayLanguage.English)]
    [InlineData(ApplicationLanguageMode.System, "fr-FR", CalendarDisplayLanguage.English)]
    public void UseAppLanguage_ResolvesPersistedPreferenceThenSystemCulture(
        ApplicationLanguageMode appLanguage,
        string cultureName,
        CalendarDisplayLanguage expected)
    {
        var resolver = new CalendarLanguageResolver(
            () => appLanguage,
            new ApplicationLanguageResolver(() => new CultureInfo(cultureName)));

        Assert.Equal(expected,
            resolver.Resolve(CalendarLanguageMode.UseAppLanguage));
    }

    [Fact]
    public void InvalidModeUsesTheSameCultureFallback()
    {
        var resolver = new CalendarLanguageResolver(
            () => ApplicationLanguageMode.System,
            new ApplicationLanguageResolver(() => new CultureInfo("he")));

        Assert.Equal(CalendarDisplayLanguage.Hebrew,
            resolver.Resolve((CalendarLanguageMode)999));
    }
}
