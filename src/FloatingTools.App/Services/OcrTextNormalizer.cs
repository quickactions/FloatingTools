using System.Text.RegularExpressions;

namespace FloatingTools.App.Services;

public static class OcrTextNormalizer
{
    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return WhitespacePattern.Replace(text, " ").Trim();
    }
}
