using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class HebrewCalendarHolidayProviderTests
{
    private readonly HebrewCalendarHolidayProvider _provider = new();

    [Theory]
    [InlineData("2024-10-03", "Rosh Hashanah", "ראש השנה")]
    [InlineData("2025-09-23", "Rosh Hashanah", "ראש השנה")]
    [InlineData("2024-10-12", "Yom Kippur", "יום כיפור")]
    [InlineData("2025-10-02", "Yom Kippur", "יום כיפור")]
    [InlineData("2024-10-17", "Sukkot", "סוכות")]
    [InlineData("2025-10-07", "Sukkot", "סוכות")]
    [InlineData("2024-12-26", "Hanukkah", "חנוכה")]
    [InlineData("2025-12-15", "Hanukkah", "חנוכה")]
    [InlineData("2024-03-24", "Purim", "פורים")]
    [InlineData("2025-03-14", "Purim", "פורים")]
    [InlineData("2024-04-23", "Passover", "פסח")]
    [InlineData("2025-04-13", "Passover", "פסח")]
    [InlineData("2024-06-12", "Shavuot", "שבועות")]
    [InlineData("2025-06-02", "Shavuot", "שבועות")]
    public void KnownGregorianDates_ReturnBilingualMajorHoliday(
        string dateText,
        string english,
        string hebrew)
    {
        var date = DateOnly.ParseExact(dateText, "yyyy-MM-dd");

        var holiday = Assert.Single(_provider.GetHolidays(date));

        Assert.Equal(date, holiday.Date);
        Assert.Equal(english, holiday.EnglishName);
        Assert.Equal(hebrew, holiday.HebrewName);
    }

    [Theory]
    [InlineData("2024-10-03", "2024-10-04", "2024-10-05")]
    [InlineData("2024-10-17", "2024-10-23", "2024-10-24")]
    [InlineData("2024-12-26", "2025-01-02", "2025-01-03")]
    [InlineData("2024-04-23", "2024-04-29", "2024-04-30")]
    public void MultiDayHolidays_IncludeFirstAndLastButNotFollowingDate(
        string firstText,
        string lastText,
        string followingText)
    {
        Assert.Single(_provider.GetHolidays(DateOnly.Parse(firstText)));
        Assert.Single(_provider.GetHolidays(DateOnly.Parse(lastText)));
        Assert.Empty(_provider.GetHolidays(DateOnly.Parse(followingText)));
    }

    [Fact]
    public void Purim_UsesAdarTwoInHebrewLeapYear()
    {
        Assert.Empty(_provider.GetHolidays(new DateOnly(2024, 2, 23)));
        Assert.Equal("Purim", Assert.Single(
            _provider.GetHolidays(new DateOnly(2024, 3, 24))).EnglishName);
    }

    [Fact]
    public void OrdinaryDateAndUnsupportedCalendarRangeReturnNoHoliday()
    {
        Assert.Empty(_provider.GetHolidays(new DateOnly(2026, 8, 31)));
        Assert.Empty(_provider.GetHolidays(DateOnly.MinValue));
    }
}
