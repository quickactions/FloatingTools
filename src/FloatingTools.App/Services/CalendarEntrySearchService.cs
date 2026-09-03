using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class CalendarEntrySearchService
{
    public IReadOnlyList<CalendarEntry> Search(
        IEnumerable<CalendarEntry> entries,
        string? query,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();
        return entries
            .Where(entry => entry is not null
                && entry.Text?.Contains(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(entry => entry.Date < today ? 1 : 0)
            .ThenBy(entry => entry.Date < today
                ? -entry.Date.DayNumber
                : entry.Date.DayNumber)
            .ThenBy(entry => entry.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id)
            .ToArray();
    }
}
