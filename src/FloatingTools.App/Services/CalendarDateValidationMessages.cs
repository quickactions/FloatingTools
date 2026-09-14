namespace FloatingTools.App.Services;

internal static class CalendarDateValidationMessages
{
    public static string InvalidDate(bool isHebrew) => isHebrew
        ? "הזינו תאריך תקין, לדוגמה 1.3.26."
        : "Enter a valid date, for example 1.3.26.";

    public static string UnsupportedDate(bool isHebrew) => isHebrew
        ? "התאריך חייב להיות בין 1.1.1900 ל־31.12.2100."
        : "Date must be between 1 Jan 1900 and 31 Dec 2100.";
}
