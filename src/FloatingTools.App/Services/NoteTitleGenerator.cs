using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FloatingTools.App.Services;

public static class NoteTitleGenerator
{
    public const string UntitledTitle = "Untitled note";
    public const int MaximumTitleLength = 40;
    public const int MaximumWords = 3;

    public static string Generate(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return UntitledTitle;
        }

        var words = Regex.Split(content.Trim(), @"\s+")
            .Where(word => word.Length > 0)
            .Take(MaximumWords);
        var title = string.Join(' ', words).Trim();
        if (title.Length > MaximumTitleLength)
        {
            title = title[..MaximumTitleLength].TrimEnd();
        }

        title = TrimTrailingPunctuation(title);

        return title.Length == 0 ? UntitledTitle : title;
    }

    private static string TrimTrailingPunctuation(string title)
    {
        var end = title.Length;
        while (end > 0
               && Rune.DecodeLastFromUtf16(title.AsSpan(0, end), out var rune, out var consumed)
                   == OperationStatus.Done
               && (Rune.IsWhiteSpace(rune) || IsRemovableTrailingPunctuation(rune)))
        {
            end -= consumed;
        }

        return title[..end];
    }

    private static bool IsRemovableTrailingPunctuation(Rune rune)
    {
        if (rune.Value is '\u05F3' or '\u05F4' or '\'' or '"'
            or '#' or '%' or '@' or '&' or '*' or '/')
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.DashPunctuation or
            UnicodeCategory.OtherPunctuation;
    }
}
