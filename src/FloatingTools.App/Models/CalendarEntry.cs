namespace FloatingTools.App.Models;

public sealed class CalendarEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly Date { get; set; }

    public string Text { get; set; } = string.Empty;
}
