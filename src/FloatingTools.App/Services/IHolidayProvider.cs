using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IHolidayProvider
{
    IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date);
}
