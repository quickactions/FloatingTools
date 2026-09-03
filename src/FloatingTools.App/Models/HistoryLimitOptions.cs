namespace FloatingTools.App.Models;

public static class HistoryLimitOptions
{
    public const int Default = 200;

    public static IReadOnlyList<int> Supported { get; } = [50, 100, 200, 500];

    public static int Normalize(int value) =>
        Supported.Contains(value) ? value : Default;
}
