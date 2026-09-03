namespace FloatingTools.App.Models;

public enum CalendarView
{
    Month,
    Week
}

public enum CalendarLanguageMode
{
    UseAppLanguage,
    English,
    Hebrew
}

public enum FirstDayOfWeekMode
{
    System,
    Sunday,
    Monday
}

public sealed class CalendarSettings
{
    public CalendarLanguageMode Language { get; set; } =
        CalendarLanguageMode.UseAppLanguage;

    public CalendarView DefaultView { get; set; } = CalendarView.Month;

    public FirstDayOfWeekMode FirstDayOfWeek { get; set; } =
        FirstDayOfWeekMode.System;

    public bool ShowHolidays { get; set; } = true;
}
