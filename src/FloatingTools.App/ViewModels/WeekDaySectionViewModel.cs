namespace FloatingTools.App.ViewModels;

public sealed record WeekDaySectionViewModel(
    DateOnly Date,
    string DateTitle,
    bool IsToday,
    bool IsSelected,
    IReadOnlyList<string> HolidayNames,
    IReadOnlyList<CalendarEntryItemViewModel> Entries)
{
    public bool HasHolidays => HolidayNames.Count > 0;

    public bool HasEntries => Entries.Count > 0;

    public bool ShowNoEvents => !HasHolidays && !HasEntries;
}
