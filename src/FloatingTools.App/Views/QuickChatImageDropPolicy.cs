using System.IO;

namespace FloatingTools.App.Views;

internal static class QuickChatImageDropPolicy
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    public static bool IsSupported(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && SupportedExtensions.Contains(Path.GetExtension(path));

    public static IReadOnlyList<string> SelectSupported(
        IEnumerable<string> paths,
        int availableCapacity)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (availableCapacity <= 0)
        {
            return [];
        }

        return paths
            .Where(IsSupported)
            .Take(availableCapacity)
            .ToArray();
    }
}
