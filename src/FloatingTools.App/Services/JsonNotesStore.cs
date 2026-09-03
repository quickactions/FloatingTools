using System.IO;
using System.Text;
using System.Text.Json;
using FloatingTools.App.Models;
using FloatingTools.App.Notes.Persistence.Migration;

namespace FloatingTools.App.Services;

public sealed class JsonNotesStore(string storagePath) : INotesStore
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<NotesStorageState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(storagePath))
            {
                return new NotesStorageState();
            }

            var json = await File.ReadAllTextAsync(
                storagePath, Encoding.UTF8, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new NotesStorageState();
            }

            var state = JsonSerializer.Deserialize<NotesStorageState>(json, Options)
                ?? new NotesStorageState();
            return NotesMigrationPipeline.Apply(state);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return new NotesStorageState();
        }
    }

    public async Task SaveAsync(
        NotesStorageState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _writeLock.WaitAsync(cancellationToken);
        var directory = Path.GetDirectoryName(storagePath);
        var temporaryPath = storagePath + ".tmp";
        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, Options);
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(false),
                cancellationToken);
            File.Move(temporaryPath, storagePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporaryPath); } catch { }
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

}
