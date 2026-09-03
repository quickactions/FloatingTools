using System.IO;
using System.Text;
using System.Text.Json;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class JsonCalendarStore : ICalendarStore
{
    public const string StorageFileName = "calendar-entries.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _storagePath;

    public JsonCalendarStore()
        : this(GetDefaultStoragePath())
    {
    }

    public JsonCalendarStore(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        _storagePath = storagePath;
    }

    public string StoragePath => _storagePath;

    public static string GetDefaultStoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            StorageFileName);

    public async Task<CalendarState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return new CalendarState();
            }

            var json = await File.ReadAllTextAsync(
                _storagePath,
                Encoding.UTF8,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CalendarState();
            }

            var state = JsonSerializer.Deserialize<CalendarState>(
                json,
                SerializerOptions);
            return Normalize(state);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return new CalendarState();
        }
    }

    public async Task SaveAsync(
        CalendarState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var persistableState = CloneForPersistence(state);
        await _writeLock.WaitAsync(cancellationToken);
        var directory = Path.GetDirectoryName(_storagePath);
        var temporaryPath = _storagePath + ".tmp";

        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(persistableState, SerializerOptions);
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            File.Move(temporaryPath, _storagePath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static CalendarState Normalize(CalendarState? state)
    {
        if (state is null
            || state.SchemaVersion < 0
            || state.SchemaVersion > CalendarState.CurrentSchemaVersion)
        {
            return new CalendarState();
        }

        state.SchemaVersion = CalendarState.CurrentSchemaVersion;
        state.Entries = state.Entries?
            .OfType<CalendarEntry>()
            .Select(CloneEntry)
            .ToList() ?? [];
        return state;
    }

    private static CalendarState CloneForPersistence(CalendarState state) =>
        new()
        {
            SchemaVersion = CalendarState.CurrentSchemaVersion,
            Entries = (state.Entries ?? [])
                .Select(entry => entry ?? throw new ArgumentException(
                    "Calendar entries cannot contain null values.",
                    nameof(state)))
                .Select(CloneEntry)
                .ToList()
        };

    private static CalendarEntry CloneEntry(CalendarEntry entry) =>
        new()
        {
            Id = entry.Id,
            Date = entry.Date,
            Text = entry.Text ?? string.Empty
        };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A later save can safely replace a stale same-directory temp file.
        }
    }
}
