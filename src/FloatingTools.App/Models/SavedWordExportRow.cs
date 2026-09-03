namespace FloatingTools.App.Models;

public sealed record SavedWordExportRow(
    string English,
    string Hebrew,
    DateTimeOffset SavedAt);
