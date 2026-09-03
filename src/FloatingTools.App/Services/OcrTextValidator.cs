using System.Text.RegularExpressions;

namespace FloatingTools.App.Services;

public static class OcrTextValidator
{
    private static readonly Regex EnglishSequencePattern = new(
        @"[A-Za-z]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool ContainsMeaningfulEnglishText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (Match match in EnglishSequencePattern.Matches(text))
        {
            if (match.Length >= 2
                || match.Value.Equals("I", StringComparison.OrdinalIgnoreCase)
                || match.Value.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
