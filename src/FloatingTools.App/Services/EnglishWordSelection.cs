namespace FloatingTools.App.Services;

public readonly record struct EnglishWordSelection(int Start, int Length, string Word);

public static class EnglishWordSelectionHelper
{
    public static bool TryExtract(
        string? text,
        int characterIndex,
        out EnglishWordSelection selection)
    {
        selection = default;
        if (string.IsNullOrEmpty(text)
            || characterIndex < 0
            || characterIndex >= text.Length
            || !IsTokenCharacter(text, characterIndex))
        {
            return false;
        }

        var start = characterIndex;
        while (start > 0 && IsTokenCharacter(text, start - 1)) start--;
        var end = characterIndex + 1;
        while (end < text.Length && IsTokenCharacter(text, end)) end++;

        while (start < end && !IsEnglishLetter(text[start])) start++;
        while (end > start && !IsEnglishLetter(text[end - 1])) end--;
        if (start >= end || HasInvalidConnector(text, start, end)) return false;
        if (start > 0 && char.IsLetter(text[start - 1])) return false;
        if (end < text.Length && char.IsLetter(text[end])) return false;
        if (IsExcludedContainingToken(text, characterIndex)) return false;

        selection = new EnglishWordSelection(start, end - start, text[start..end]);
        return true;
    }

    private static bool IsTokenCharacter(string text, int index)
    {
        var character = text[index];
        return IsEnglishLetter(character)
            || character is '\'' or '\u2019' or '-';
    }

    private static bool HasInvalidConnector(string text, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (text[index] is not ('\'' or '\u2019' or '-')) continue;
            if (index == start || index + 1 >= end
                || !IsEnglishLetter(text[index - 1])
                || !IsEnglishLetter(text[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExcludedContainingToken(string text, int characterIndex)
    {
        var start = characterIndex;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        var end = characterIndex + 1;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        var token = text[start..end].Trim('"', '\'', '\u2018', '\u2019', '(', ')', '[', ']', '{', '}', ',', ';', ':', '!', '?', '.');
        if (token.Contains("://", StringComparison.Ordinal)
            || token.Contains('@')
            || token.Any(char.IsDigit)
            || token.Contains('+')
            || token.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            || token.Contains('/'))
        {
            return true;
        }

        var dot = token.LastIndexOf('.');
        return dot > 0
            && dot + 2 < token.Length
            && token[..dot].Any(IsEnglishLetter)
            && token[(dot + 1)..].All(IsEnglishLetter);
    }

    private static bool IsEnglishLetter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}