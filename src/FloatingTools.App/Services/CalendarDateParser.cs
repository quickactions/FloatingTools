using System.Globalization;
using System.Text.RegularExpressions;

namespace FloatingTools.App.Services;

public static partial class CalendarDateParser
{
    public static bool TryParse(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = DatePattern().Match(text);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var day)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(match.Groups[3].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (match.Groups[3].Value.Length == 2)
        {
            year += 2000;
        }

        return DateOnly.TryParseExact(
            $"{day:D2}/{month:D2}/{year:D4}",
            "dd/MM/yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    [GeneratedRegex(
        @"^\s*(\d{1,2})(?:\s*[/.-]\s*|\s+)(\d{1,2})(?:\s*[/.-]\s*|\s+)(\d{2}|\d{4})\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}
