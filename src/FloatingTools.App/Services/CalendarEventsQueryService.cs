using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class CalendarEventsQueryService
{
    public IReadOnlyList<CalendarEntry> Query(
        IEnumerable<CalendarEntry> entries,
        DateOnly from,
        DateOnly to,
        bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (to < from)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(to));
        }

        var inRange = entries.Where(entry => entry.Date >= from && entry.Date <= to);
        return (descending
                ? inRange.OrderByDescending(entry => entry.Date)
                : inRange.OrderBy(entry => entry.Date))
            .ToArray();
    }
}
