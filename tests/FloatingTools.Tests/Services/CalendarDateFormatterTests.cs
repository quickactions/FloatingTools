using System.Globalization;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class CalendarDateFormatterTests
{
    private static readonly DateOnly Date = new(2026, 9, 23);
    private static readonly CultureInfo English = new("en-US");
    private static readonly CultureInfo Hebrew = CreateHebrewGregorianCulture();

    [Fact]
    public void EnglishFormatsMonthBeforeDay()
    {
        Assert.Equal("Sep 23", CalendarDateFormatter.FormatCompactDate(
            Date, CalendarDisplayLanguage.English, English));
        Assert.Equal("Wednesday, September 23, 2026",
            CalendarDateFormatter.FormatDayPanelDate(
                Date, CalendarDisplayLanguage.English, English));
        Assert.Equal("Wednesday, September 23",
            CalendarDateFormatter.FormatWeekHeading(
                Date, CalendarDisplayLanguage.English, English));
        Assert.Equal("Sep 23, 2026",
            CalendarDateFormatter.FormatSearchResultDate(
                Date, CalendarDisplayLanguage.English, English));
    }

    [Fact]
    public void HebrewFormatsDayBeforePrefixedMonth()
    {
        Assert.StartsWith("23 ב", CalendarDateFormatter.FormatCompactDate(
            Date, CalendarDisplayLanguage.Hebrew, Hebrew));
        Assert.Contains("23 בספטמבר 2026",
            CalendarDateFormatter.FormatDayPanelDate(
                Date, CalendarDisplayLanguage.Hebrew, Hebrew));
        Assert.Contains("23 בספטמבר",
            CalendarDateFormatter.FormatWeekHeading(
                Date, CalendarDisplayLanguage.Hebrew, Hebrew));
        Assert.StartsWith("23 ב", CalendarDateFormatter.FormatSearchResultDate(
            Date, CalendarDisplayLanguage.Hebrew, Hebrew));
    }

    [Fact]
    public void HebrewSameMonthWeekRangeGroupsDaysBeforeTheSharedMonth()
    {
        var start = new DateOnly(2026, 8, 9);
        var end = new DateOnly(2026, 8, 15);

        Assert.Equal("9–15 באוג׳", CalendarDateFormatter.FormatWeekRange(
            start, end, CalendarDisplayLanguage.Hebrew, Hebrew));
    }

    [Fact]
    public void EnglishSameMonthWeekRangeIsUnchanged()
    {
        var start = new DateOnly(2026, 8, 9);
        var end = new DateOnly(2026, 8, 15);

        Assert.Equal("Aug 9 – 15", CalendarDateFormatter.FormatWeekRange(
            start, end, CalendarDisplayLanguage.English, English));
    }

    [Fact]
    public void HebrewCrossMonthWeekRangeUsesCleanLogicalText()
    {
        var start = new DateOnly(2026, 8, 30);
        var end = new DateOnly(2026, 9, 5);

        var result = CalendarDateFormatter.FormatWeekRange(
            start, end, CalendarDisplayLanguage.Hebrew, Hebrew);

        Assert.Equal("30 באוג׳ – 5 בספט׳", result);
        Assert.DoesNotContain('\u2066', result);
        Assert.DoesNotContain('\u2069', result);
    }

    [Fact]
    public void HebrewSameMonthWeekTitlePartsSeparateLtrRangeFromRtlMonth()
    {
        var parts = CalendarDateFormatter.FormatHebrewWeekTitleParts(
            new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 15), Hebrew);

        Assert.Equal("9–15", parts.StartNumericText);
        Assert.Equal("באוג׳", parts.StartMonthText);
        Assert.False(parts.IsCrossMonth);
        Assert.Null(parts.EndNumericText);
        Assert.Null(parts.EndMonthText);
    }

    [Fact]
    public void HebrewCrossMonthWeekTitlePartsSeparateBothDates()
    {
        var parts = CalendarDateFormatter.FormatHebrewWeekTitleParts(
            new DateOnly(2026, 8, 30), new DateOnly(2026, 9, 5), Hebrew);

        Assert.Equal("30", parts.StartNumericText);
        Assert.Equal("באוג׳", parts.StartMonthText);
        Assert.Equal("5", parts.EndNumericText);
        Assert.Equal("בספט׳", parts.EndMonthText);
        Assert.True(parts.IsCrossMonth);
    }

    [Fact]
    public void EnglishCrossMonthWeekRangeIsUnchanged()
    {
        var start = new DateOnly(2026, 8, 30);
        var end = new DateOnly(2026, 9, 5);

        Assert.Equal("Aug 30 – Sep 5", CalendarDateFormatter.FormatWeekRange(
            start, end, CalendarDisplayLanguage.English, English));
    }

    private static CultureInfo CreateHebrewGregorianCulture()
    {
        var culture = (CultureInfo)new CultureInfo("he-IL").Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return culture;
    }
}
