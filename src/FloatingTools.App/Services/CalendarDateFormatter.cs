using System.Globalization;

namespace FloatingTools.App.Services;

public static class CalendarDateFormatter
{
    public static string FormatCompactDate(
        DateOnly date,
        CalendarDisplayLanguage language,
        CultureInfo culture) => Format(
            date,
            language == CalendarDisplayLanguage.Hebrew ? "d בMMM" : "MMM d",
            culture);

    public static string FormatWeekHeading(
        DateOnly date,
        CalendarDisplayLanguage language,
        CultureInfo culture) => Format(
            date,
            language == CalendarDisplayLanguage.Hebrew
                ? "dddd, d בMMMM"
                : "dddd, MMMM d",
            culture);

    public static string FormatDayPanelDate(
        DateOnly date,
        CalendarDisplayLanguage language,
        CultureInfo culture) => Format(
            date,
            language == CalendarDisplayLanguage.Hebrew
                ? "dddd, d בMMMM yyyy"
                : "dddd, MMMM d, yyyy",
            culture);

    public static string FormatSearchResultDate(
        DateOnly date,
        CalendarDisplayLanguage language,
        CultureInfo culture) => Format(
            date,
            language == CalendarDisplayLanguage.Hebrew
                ? "d בMMM yyyy"
                : "MMM d, yyyy",
            culture);

    public static string FormatMonthYear(DateOnly date, CultureInfo culture) =>
        Format(date, "MMMM yyyy", culture);

    public static string FormatWeekRange(
        DateOnly start,
        DateOnly end,
        CalendarDisplayLanguage language,
        CultureInfo culture)
    {
        var sameMonth = start.Month == end.Month && start.Year == end.Year;

        // Hebrew reads right-to-left, so a same-month range must group the
        // day numbers together ("9–15") before the shared month ("באוג׳"),
        // rather than attaching the month to the start day only.
        if (language == CalendarDisplayLanguage.Hebrew && sameMonth)
        {
            var monthText = Format(end, "בMMM", culture);
            return $"{start.Day}–{end.Day} {monthText}";
        }

        var startText = FormatCompactDate(start, language, culture);
        var endText = sameMonth
            ? end.Day.ToString(culture)
            : FormatCompactDate(end, language, culture);

        return $"{startText} – {endText}";
    }

    /// <summary>
    /// Produces the logical text fragments for the Hebrew Week header. The
    /// Calendar view renders these fragments in explicit WPF directional
    /// inline scopes instead of relying on Unicode directional controls.
    /// </summary>
    public static HebrewWeekTitleParts FormatHebrewWeekTitleParts(
        DateOnly start,
        DateOnly end,
        CultureInfo culture)
    {
        var sameMonth = start.Month == end.Month && start.Year == end.Year;
        if (sameMonth)
        {
            return new HebrewWeekTitleParts(
                $"{start.Day}–{end.Day}",
                Format(end, "בMMM", culture),
                null,
                null);
        }

        return new HebrewWeekTitleParts(
            start.Day.ToString(culture),
            Format(start, "בMMM", culture),
            end.Day.ToString(culture),
            Format(end, "בMMM", culture));
    }

    private static string Format(DateOnly date, string format, CultureInfo culture) =>
        date.ToDateTime(TimeOnly.MinValue).ToString(format, culture);
}

/// <summary>
/// Hebrew Week-header fragments in their logical, non-isolated form.
/// A null end pair denotes a same-month range with one shared month label.
/// </summary>
public sealed record HebrewWeekTitleParts(
    string StartNumericText,
    string StartMonthText,
    string? EndNumericText,
    string? EndMonthText)
{
    public bool IsCrossMonth => EndNumericText is not null;
}
