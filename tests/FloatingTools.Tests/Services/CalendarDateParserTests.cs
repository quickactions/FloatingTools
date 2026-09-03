using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class CalendarDateParserTests
{
    [Theory]
    [InlineData("1/3/26", 2026, 3, 1)]
    [InlineData("01/03/26", 2026, 3, 1)]
    [InlineData("1/3/2026", 2026, 3, 1)]
    [InlineData("01/03/2026", 2026, 3, 1)]
    [InlineData("1.3.26", 2026, 3, 1)]
    [InlineData("01.03.2026", 2026, 3, 1)]
    [InlineData("1-3-26", 2026, 3, 1)]
    [InlineData("01-03-2026", 2026, 3, 1)]
    [InlineData("1 3 26", 2026, 3, 1)]
    [InlineData("01 03 2026", 2026, 3, 1)]
    [InlineData(" 1 / 3 / 26 ", 2026, 3, 1)]
    [InlineData("29/2/24", 2024, 2, 29)]
    [InlineData("1/1/00", 2000, 1, 1)]
    [InlineData("1/1/99", 2099, 1, 1)]
    [InlineData("1/1/1999", 1999, 1, 1)]
    public void TryParse_AcceptsApprovedDayMonthYearFormats(
        string text,
        int year,
        int month,
        int day)
    {
        var original = text;

        var parsed = CalendarDateParser.TryParse(text, out var date);

        Assert.True(parsed);
        Assert.Equal(new DateOnly(year, month, day), date);
        Assert.Equal(original, text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("29/2/23")]
    [InlineData("31/4/26")]
    [InlineData("0/3/26")]
    [InlineData("1/0/26")]
    [InlineData("1/13/26")]
    [InlineData("13/31/26")]
    [InlineData("1/3")]
    [InlineData("1/3/2")]
    [InlineData("1/3/026")]
    [InlineData("1/3/20261")]
    [InlineData("1/3/26/4")]
    [InlineData("1/3/26 extra")]
    [InlineData("not a date")]
    public void TryParse_RejectsInvalidOrMalformedInput(string? text)
    {
        Assert.False(CalendarDateParser.TryParse(text, out var date));
        Assert.Equal(default, date);
    }
}
