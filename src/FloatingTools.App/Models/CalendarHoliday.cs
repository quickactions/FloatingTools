namespace FloatingTools.App.Models;

public sealed record CalendarHoliday(
    DateOnly Date,
    string EnglishName,
    string HebrewName);
