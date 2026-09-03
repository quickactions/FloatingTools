using System.Globalization;
using System.Text;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class SavedWordsExportFormatter
{
    public static string CreateCsv(
        IReadOnlyList<SavedWord> items,
        bool includeSavedAt = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        var builder = new StringBuilder(includeSavedAt
            ? "English,Hebrew,SavedAt\r\n"
            : "English,Hebrew\r\n");
        foreach (var item in SavedWordExportNormalizer.Normalize(items))
        {
            builder.Append(EscapeCsv(item.English)).Append(',')
                .Append(EscapeCsv(item.Hebrew));
            if (includeSavedAt)
            {
                builder.Append(',')
                    .Append(EscapeCsv(item.SavedAt.ToString(
                        "O",
                        CultureInfo.InvariantCulture)));
            }

            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    public static string CreateText(IReadOnlyList<SavedWord> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return string.Join(
            Environment.NewLine,
            SavedWordExportNormalizer.Normalize(items)
                .Select(item => $"{item.English} — {item.Hebrew}"));
    }

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";
}
