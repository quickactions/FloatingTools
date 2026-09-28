using System.IO;
using System.Text;
using System.Text.Json;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

/// <summary>Bounded history uses the same UTF-8/temp-file replacement convention
/// as the other local JSON stores. Mutations are serialized, including clear/trim.
/// Only TranslationEntry data is serialized; view-model state is never stored.</summary>
public sealed class JsonTranslationHistoryStore(string storagePath) : ITranslationHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Lock _syncRoot = new();
    private List<TranslationEntry>? _entries;

    public Task<IReadOnlyList<TranslationEntry>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<TranslationEntry>>(
                Entries.OrderBy(entry => entry.CreatedAt).ToArray());
        }
    }

    public Task AddOrUpdateAsync(TranslationEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            var updated = Entries.ToList();
            var index = updated.FindIndex(existing => existing.Id == entry.Id);
            if (index < 0) updated.Add(entry);
            else updated[index] = entry;
            Save(updated);
        }
        return Task.CompletedTask;
    }

    public void TrimToLimit(int maximumEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        lock (_syncRoot)
        {
            if (Entries.Count <= maximumEntries) return;
            Save(Entries.OrderBy(entry => entry.CreatedAt).TakeLast(maximumEntries).ToList());
        }
    }

    public void Clear()
    {
        lock (_syncRoot) Save([]);
    }

    public void Remove(Guid entryId)
    {
        lock (_syncRoot)
        {
            if (!Entries.Any(entry => entry.Id == entryId)) return;
            Save(Entries.Where(entry => entry.Id != entryId).ToList());
        }
    }

    private List<TranslationEntry> Entries => _entries ??= Read();

    private List<TranslationEntry> Read()
    {
        try
        {
            if (!File.Exists(storagePath)) return [];
            var json = File.ReadAllText(storagePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return [];
            return (JsonSerializer.Deserialize<List<TranslationEntry>>(json, SerializerOptions) ?? [])
                .Where(entry => entry is not null && entry.Id != Guid.Empty
                    && !string.IsNullOrWhiteSpace(entry.SourceText) && entry.Result is not null
                    && (entry.Result.CorrectionStatus == TranslationCorrectionStatus.Ambiguous
                        || !string.IsNullOrWhiteSpace(entry.Result.MainTranslation)))
                .GroupBy(entry => entry.Id).Select(group => group.Last())
                .OrderBy(entry => entry.CreatedAt).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or NotSupportedException or ArgumentException)
        {
            return [];
        }
    }

    private void Save(List<TranslationEntry> entries)
    {
        var temporaryPath = storagePath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(storagePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var ordered = entries.OrderBy(entry => entry.CreatedAt).ToList();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(ordered, SerializerOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, storagePath, overwrite: true);
            _entries = ordered;
        }
        catch
        {
            try { File.Delete(temporaryPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }
}
