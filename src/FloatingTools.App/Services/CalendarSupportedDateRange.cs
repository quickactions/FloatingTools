namespace FloatingTools.App.Services;

public static class CalendarSupportedDateRange
{
    public static readonly DateOnly Minimum = new(1900, 1, 1);

    public static readonly DateOnly Maximum = new(2100, 12, 31);

    public static bool Contains(DateOnly date) => date >= Minimum && date <= Maximum;
}
