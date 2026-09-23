using System.IO;
using System.Diagnostics;
using System.Windows;
using FloatingTools.App.Models;
using Microsoft.Win32;

namespace FloatingTools.App.Services;

public sealed class WindowsNoteExportService : INoteExportService
{
    public async Task<bool> ExportAsync(
        NoteDocument note,
        NoteExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);
        var title = string.IsNullOrWhiteSpace(note.Title) ? "Untitled note" : note.Title.Trim();
        var stem = SanitizeFileName(title);
        var options = GetFileOptions(note, format);
        var dialog = new SaveFileDialog
        {
            Title = $"Export {title}",
            Filter = options.Filter,
            DefaultExt = options.Extension,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = stem + options.Suffix
        };
        try
        {
            var owner = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive)
                ?? Application.Current?.MainWindow;
            var accepted = owner is null
                ? dialog.ShowDialog()
                : dialog.ShowDialog(owner);
            if (accepted != true)
            {
                return false;
            }

            var result = format switch
            {
                NoteExportFormat.Pdf => NotePdfExporter.Create(note),
                NoteExportFormat.Word => NoteWordExporter.Create(note),
                NoteExportFormat.Markdown => NoteMarkdownExporter.Create(note, stem),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
            await File.WriteAllBytesAsync(dialog.FileName, result.Content, cancellationToken);
            if (result.HasMissingOrUnreadableImages)
            {
                MessageBox.Show(
                    "The note was exported, but one or more images could not be read.",
                    "FloatingTools",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or NotSupportedException
            or InvalidOperationException
            or System.Runtime.InteropServices.COMException)
        {
            Debug.WriteLine($"Notes export failed: {exception.GetType().Name}: {exception.Message}");
            MessageBox.Show(
                "Could not export note.",
                "FloatingTools",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    internal static NoteExportFileOptions GetFileOptions(NoteDocument note, NoteExportFormat format) =>
        format switch
        {
            NoteExportFormat.Pdf => new("PDF files (*.pdf)|*.pdf", ".pdf", ".pdf"),
            NoteExportFormat.Word => new("Word documents (*.docx)|*.docx", ".docx", ".docx"),
            NoteExportFormat.Markdown when NoteMarkdownExporter.HasImageBlocks(note) =>
                new("ZIP archives (*.zip)|*.zip", ".zip", ".zip"),
            NoteExportFormat.Markdown => new("Markdown files (*.md)|*.md", ".md", ".md"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    internal static string SanitizeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var characters = title
            .Trim()
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray();
        var result = new string(characters).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "Untitled note" : result;
    }
}

internal sealed record NoteExportFileOptions(string Filter, string Extension, string Suffix);
