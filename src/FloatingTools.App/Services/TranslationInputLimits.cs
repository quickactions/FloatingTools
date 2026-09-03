namespace FloatingTools.App.Services;

public static class TranslationInputLimits
{
    public const int MaximumWordCount = 30;
    public const string MaximumWordCountMessage =
        "Maximum 30 words per translation.";

    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        var insideWord = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                insideWord = false;
                continue;
            }

            if (!insideWord)
            {
                count++;
                insideWord = true;
            }
        }

        return count;
    }

    public static bool IsWithinWordLimit(string? text) =>
        CountWords(text) <= MaximumWordCount;
}
