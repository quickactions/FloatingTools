using System.Globalization;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class HebrewCalendarHolidayProvider : IHolidayProvider
{
    private static readonly HebrewCalendar Calendar = new();

    public IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date)
    {
        try
        {
            var value = date.ToDateTime(TimeOnly.MinValue);
            var year = Calendar.GetYear(value);
            var month = Calendar.GetMonth(value);
            var day = Calendar.GetDayOfMonth(value);
            var isLeapYear = Calendar.IsLeapYear(year);
            var purimMonth = isLeapYear ? 7 : 6;
            var nisan = isLeapYear ? 8 : 7;
            var sivan = isLeapYear ? 10 : 9;

            if (month == 1 && day is 1 or 2)
            {
                return One(date, "Rosh Hashanah", "ראש השנה");
            }

            if (month == 1 && day == 10)
            {
                return One(date, "Yom Kippur", "יום כיפור");
            }

            if (month == 1 && day is >= 15 and <= 21)
            {
                return One(date, "Sukkot", "סוכות");
            }

            var hanukkahStart = DateOnly.FromDateTime(Calendar.ToDateTime(
                year, 3, 25, 0, 0, 0, 0));
            if (date >= hanukkahStart && date <= hanukkahStart.AddDays(7))
            {
                return One(date, "Hanukkah", "חנוכה");
            }

            if (month == purimMonth && day == 14)
            {
                return One(date, "Purim", "פורים");
            }

            if (month == nisan && day is >= 15 and <= 21)
            {
                return One(date, "Passover", "פסח");
            }

            if (month == sivan && day == 6)
            {
                return One(date, "Shavuot", "שבועות");
            }

            return [];
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    private static IReadOnlyList<CalendarHoliday> One(
        DateOnly date,
        string englishName,
        string hebrewName) =>
        [new CalendarHoliday(date, englishName, hebrewName)];
}
