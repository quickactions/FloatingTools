namespace FloatingTools.App.ViewModels;

public sealed record CalendarDayCellViewModel(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    bool IsSelected,
    bool IsHoliday,
    bool HasEvents,
    bool IsSupported)
{
    public int DayNumber => Date.Day;
}
