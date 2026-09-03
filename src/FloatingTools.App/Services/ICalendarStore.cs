using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface ICalendarStore
{
    Task<CalendarState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        CalendarState state,
        CancellationToken cancellationToken = default);
}
