namespace FloatingTools.App.SharedUi.Direction;

public enum TextDirection
{
    Neutral,
    LeftToRight,
    RightToLeft
}

public enum TextDirectionAlignment
{
    Default,
    Left,
    Right
}

public readonly record struct TextDirectionResolution(
    TextDirection Direction,
    TextDirectionAlignment Alignment);

/// <summary>
/// Resolves Hebrew/English text direction using the first strong character.
/// Hebrew strength is the standard Hebrew letter range U+05D0-U+05EA;
/// English strength is ASCII A-Z or a-z. All other characters, including
/// whitespace, digits, punctuation, and Hebrew punctuation, are neutral.
/// </summary>
public static class TextDirectionResolver
{
    private static readonly TextDirectionResolution Neutral =
        new(TextDirection.Neutral, TextDirectionAlignment.Default);

    private static readonly TextDirectionResolution LeftToRight =
        new(TextDirection.LeftToRight, TextDirectionAlignment.Left);

    private static readonly TextDirectionResolution RightToLeft =
        new(TextDirection.RightToLeft, TextDirectionAlignment.Right);

    public static TextDirectionResolution Resolve(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Neutral;
        }

        foreach (var character in text)
        {
            if (IsHebrewLetter(character))
            {
                return RightToLeft;
            }

            if (IsEnglishLetter(character))
            {
                return LeftToRight;
            }
        }

        return Neutral;
    }

    private static bool IsHebrewLetter(char character) =>
        character is >= '\u05D0' and <= '\u05EA';

    private static bool IsEnglishLetter(char character) =>
        character is >= 'A' and <= 'Z'
        || character is >= 'a' and <= 'z';
}
