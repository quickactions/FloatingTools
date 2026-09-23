using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class NoteMarkdownExporter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly HashSet<char> MarkdownPunctuation =
        ['\\', '`', '*', '_', '{', '}', '[', ']', '<', '>', '#', '+', '-', '!', '|', '~'];

    public static bool HasImageBlocks(NoteDocument note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return note.Blocks.OfType<ImageNoteBlock>().Any();
    }

    public static NoteExportResult Create(NoteDocument note, string markdownFileNameStem)
    {
        ArgumentNullException.ThrowIfNull(note);
        var stem = SafeZipFileName(markdownFileNameStem);
        return HasImageBlocks(note)
            ? CreateZip(note, stem)
            : new NoteExportResult(Utf8WithoutBom.GetBytes(CreateMarkdown(note, null)), false);
    }

    private static NoteExportResult CreateZip(NoteDocument note, string stem)
    {
        using var output = new MemoryStream();
        var images = new List<ExportedImage>();
        var hadImageError = false;
        var markdown = CreateMarkdown(note, image =>
        {
            var number = images.Count + 1;
            if (TryReadImage(image, number, out var exported))
            {
                images.Add(exported);
                return new ImageMarkdown(number, exported.EntryPath, IsAvailable: true);
            }

            hadImageError = true;
            images.Add(new ExportedImage(number, string.Empty, []));
            return new ImageMarkdown(number, string.Empty, IsAvailable: false);
        });

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Utf8WithoutBom))
        {
            WriteEntry(archive, $"{stem}.md", Utf8WithoutBom.GetBytes(markdown));
            foreach (var image in images.Where(candidate => candidate.Bytes.Length > 0))
            {
                WriteEntry(archive, image.EntryPath, image.Bytes);
            }
        }

        return new NoteExportResult(output.ToArray(), hadImageError);
    }

    private static string CreateMarkdown(
        NoteDocument note,
        Func<ImageNoteBlock, ImageMarkdown>? exportImage)
    {
        var sections = new List<string>
        {
            $"# {EscapeInline(GetTitle(note).Replace('\r', ' ').Replace('\n', ' '))}"
        };
        var imageNumber = 0;
        foreach (var block in note.Blocks)
        {
            switch (block)
            {
                case TextNoteBlock text:
                    sections.Add(EscapeTextBlock(text.Text));
                    break;

                case ImageNoteBlock image:
                    imageNumber++;
                    var result = exportImage?.Invoke(image)
                        ?? new ImageMarkdown(imageNumber, string.Empty, IsAvailable: false);
                    var label = $"תמונה {imageNumber}";
                    sections.Add(result.IsAvailable
                        ? $"[{label}]{Environment.NewLine}![{label}]({result.EntryPath})"
                        : $"[{label}]{Environment.NewLine}> **Image unavailable:** Image {imageNumber}");
                    break;

                case LinkListNoteBlock links:
                    sections.Add(string.Join(Environment.NewLine,
                        links.Items.Select(item =>
                            $"- [{EscapeLinkLabel(item.VisibleName)}]({EscapeLinkDestination(item.Url)})")));
                    break;
            }
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections)
            + Environment.NewLine;
    }

    private static string EscapeTextBlock(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var result = new StringBuilder();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = EscapeInline(lines[index]);
            result.Append(line);
            if (index == lines.Length - 1)
            {
                continue;
            }

            if (lines[index].Length > 0 && lines[index + 1].Length > 0)
            {
                result.Append("  ");
            }

            result.Append(Environment.NewLine);
        }

        return result.ToString();
    }

    private static string EscapeInline(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (MarkdownPunctuation.Contains(character))
            {
                result.Append('\\');
            }

            result.Append(character);
        }

        return EscapeOrderedListMarker(result.ToString());
    }

    private static string EscapeOrderedListMarker(string line)
    {
        var index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index])) index++;
        while (index < line.Length && char.IsDigit(line[index])) index++;
        if (index == 0 || index >= line.Length || line[index] is not ('.' or ')')) return line;
        if (index + 1 >= line.Length || !char.IsWhiteSpace(line[index + 1])) return line;
        return line.Insert(index, "\\");
    }

    private static string EscapeLinkLabel(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal)
        .Replace("]", "\\]", StringComparison.Ordinal);

    private static string EscapeLinkDestination(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal)
        .Replace(" ", "%20", StringComparison.Ordinal);

    private static bool TryReadImage(ImageNoteBlock image, int number, out ExportedImage exported)
    {
        exported = null!;
        if (string.IsNullOrWhiteSpace(image.AssetPath) || !File.Exists(image.AssetPath)) return false;
        try
        {
            var bytes = File.ReadAllBytes(image.AssetPath);
            using var stream = new MemoryStream(bytes, writable: false);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return false;

            string extension;
            if (decoder is PngBitmapDecoder)
            {
                extension = ".png";
            }
            else if (decoder is JpegBitmapDecoder)
            {
                var sourceExtension = Path.GetExtension(image.AssetPath).ToLowerInvariant();
                extension = sourceExtension == ".jpeg" ? ".jpeg" : ".jpg";
            }
            else if (decoder is BmpBitmapDecoder)
            {
                extension = ".png";
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(decoder.Frames[0]);
                using var converted = new MemoryStream();
                encoder.Save(converted);
                bytes = converted.ToArray();
            }
            else
            {
                return false;
            }

            exported = new ExportedImage(number, $"images/image-{number:000}{extension}", bytes);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or NotSupportedException
            or InvalidOperationException)
        {
            return false;
        }
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string SafeZipFileName(string value)
    {
        var safe = Path.GetFileName(value).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(safe) ? "Untitled note" : safe;
    }

    private static string GetTitle(NoteDocument note) =>
        string.IsNullOrWhiteSpace(note.Title) ? "Untitled note" : note.Title.Trim();

    private sealed record ImageMarkdown(int Number, string EntryPath, bool IsAvailable);
    private sealed record ExportedImage(int Number, string EntryPath, byte[] Bytes);
}