using System.Text.RegularExpressions;

namespace FloatingTools.App.Services;

public static class NoteTitleGenerator
{
    public const string UntitledTitle = "Untitled note";
    public const int MaximumTitleLength = 40;
    public const int MaximumWords = 4;

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

        return title.Length == 0 ? UntitledTitle : title;
    }
}
