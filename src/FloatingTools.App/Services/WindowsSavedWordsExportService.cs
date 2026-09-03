using System.IO;
using System.Text;
using FloatingTools.App.Models;
using Microsoft.Win32;

namespace FloatingTools.App.Services;

public sealed class WindowsSavedWordsExportService : ISavedWordsExportService
{
    public async Task<bool> ExportAsync(
        IReadOnlyList<SavedWord> items,
        SavedWordsExportFormat format,
        string title = "Saved Words",
        string fileNameStem = "saved-words",
        bool includeSavedAt = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var (filter, defaultExtension, fileName) = format switch
        {
            SavedWordsExportFormat.Csv =>
                ("CSV files (*.csv)|*.csv", ".csv", $"{fileNameStem}.csv"),
            SavedWordsExportFormat.Text =>
                ("Text files (*.txt)|*.txt", ".txt", $"{fileNameStem}.txt"),
            SavedWordsExportFormat.Pdf =>
                ("PDF files (*.pdf)|*.pdf", ".pdf", $"{fileNameStem}.pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
        var dialog = new SaveFileDialog
        {
            Title = $"Export {title}",
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = fileName
        };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        if (format == SavedWordsExportFormat.Pdf)
        {
            var pdf = SavedWordsPdfExporter.Create(items, title);
            await File.WriteAllBytesAsync(dialog.FileName, pdf, cancellationToken);
            return true;
        }

        var content = format == SavedWordsExportFormat.Csv
            ? SavedWordsExportFormatter.CreateCsv(items, includeSavedAt)
            : SavedWordsExportFormatter.CreateText(items);
        await File.WriteAllTextAsync(
            dialog.FileName,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            cancellationToken);
        return true;
    }
}
