namespace FloatingTools.App.ViewModels;

public sealed record DayPanelViewModel(
    DateOnly? Date,
    string DateTitle,
    IReadOnlyList<string> HolidayNames,
    IReadOnlyList<CalendarEntryItemViewModel> Entries,
    string EmptyStateText,
    string NoEventsText)
{
    public bool HasSelection => Date.HasValue;

    public bool HasHolidays => HolidayNames.Count > 0;

    public bool HasEntries => Entries.Count > 0;

    // A holiday is useful day content on its own, so the empty event label is omitted.
    public bool ShowNoEvents => HasSelection && !HasHolidays && !HasEntries;
}
