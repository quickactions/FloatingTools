namespace FloatingTools.App.Services;

public static class LinkTokenParser
{
    public static IReadOnlyList<string> Parse(string? input) =>
        string.IsNullOrWhiteSpace(input)
            ? []
            : input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public static class LinkUrlValidator
{
    public static bool TryValidate(string? value, out string url)
    {
        url = value?.Trim() ?? string.Empty;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}
