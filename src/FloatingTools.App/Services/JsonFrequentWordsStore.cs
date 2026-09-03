using System.IO;
using System.Text;
using System.Text.Json;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class JsonFrequentWordsStore(string storagePath) : IFrequentWordsStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public async Task<IReadOnlyList<FrequentWord>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(storagePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(
                storagePath,
                Encoding.UTF8,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<FrequentWord>>(
                       json,
                       SerializerOptions)?
                   .Where(IsValid)
                   .ToArray()
                ?? [];
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return [];
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<FrequentWord> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var directory = Path.GetDirectoryName(storagePath);
        var temporaryPath = storagePath + ".tmp";
        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(items, SerializerOptions);
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            File.Move(temporaryPath, storagePath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static bool IsValid(FrequentWord item) =>
        item.Id != Guid.Empty
        && !string.IsNullOrWhiteSpace(item.NormalizedSourceKey)
        && !string.IsNullOrWhiteSpace(item.SourceText)
        && !string.IsNullOrWhiteSpace(item.PrimaryTranslation)
        && !string.IsNullOrWhiteSpace(item.SourceLanguage)
        && !string.IsNullOrWhiteSpace(item.TargetLanguage)
        && item.UsageCount > 0;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The next save can safely replace a stale temporary file.
        }
    }
}
