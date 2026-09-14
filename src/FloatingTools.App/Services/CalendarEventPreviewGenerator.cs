using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public readonly record struct CalendarEventPreview(string Text, bool IsTruncated);

public sealed class CalendarEventPreviewGenerator
{
    public const int MaximumWords = 3;
    public const int MaximumCharacters = 32;
    public const int LargeMaximumWords = 7;
    public const int LargeMaximumCharacters = 72;

    public CalendarEventPreview Generate(
        string? content,
        CalendarLayoutMode layoutMode = CalendarLayoutMode.Compact)
    {
        var (maximumWords, maximumCharacters) = layoutMode switch
        {
            CalendarLayoutMode.Compact => (MaximumWords, MaximumCharacters),
            CalendarLayoutMode.Large => (LargeMaximumWords, LargeMaximumCharacters),
            _ => throw new ArgumentOutOfRangeException(nameof(layoutMode))
        };
        var words = (content ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return new(string.Empty, false);
        }

        var preview = string.Join(' ', words.Take(maximumWords));
        var truncated = words.Length > maximumWords || preview.Length > maximumCharacters;
        if (!truncated)
        {
            return new(preview, false);
        }

        var contentLength = Math.Min(preview.Length, maximumCharacters - 1);
        return new(preview[..contentLength].TrimEnd() + '…', true);
    }
}
