namespace FloatingTools.App.Models;

public sealed class CalendarState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<CalendarEntry> Entries { get; set; } = [];
}
