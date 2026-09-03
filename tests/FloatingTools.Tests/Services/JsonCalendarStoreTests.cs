using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class JsonCalendarStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MissingFile_ReturnsCurrentEmptyStateWithoutCreatingStorage()
    {
        var store = CreateStore();

        var state = await store.LoadAsync();

        Assert.Equal(CalendarState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Empty(state.Entries);
        Assert.False(File.Exists(store.StoragePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ malformed")]
    public async Task EmptyOrMalformedFile_ReturnsSafeEmptyState(string json)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(GetStoragePath(), json);

        var state = await CreateStore().LoadAsync();

        Assert.Equal(CalendarState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Empty(state.Entries);
    }

    [Fact]
    public async Task RoundTrip_PreservesDatesIdsUnicodeAndSameDateEntries()
    {
        var date = new DateOnly(2026, 3, 1);
        var first = Entry(date, "פגישה חשובה");
        var second = Entry(date, "Dentist");
        var store = CreateStore();

        await store.SaveAsync(new CalendarState { Entries = [first, second] });
        var restored = await store.LoadAsync();

        Assert.Equal([first.Id, second.Id], restored.Entries.Select(entry => entry.Id));
        Assert.All(restored.Entries, entry => Assert.Equal(date, entry.Date));
        Assert.Equal(["פגישה חשובה", "Dentist"],
            restored.Entries.Select(entry => entry.Text));
        Assert.False(File.Exists(store.StoragePath + ".tmp"));
    }

    [Fact]
    public async Task Save_OverwritesAtomicallyAndLeavesNoTemporaryFile()
    {
        var store = CreateStore();
        await store.SaveAsync(new CalendarState
        {
            Entries = [Entry(new DateOnly(2026, 1, 1), "old")]
        });

        await store.SaveAsync(new CalendarState
        {
            Entries = [Entry(new DateOnly(2027, 2, 2), "new")]
        });

        var restored = Assert.Single((await store.LoadAsync()).Entries);
        Assert.Equal("new", restored.Text);
        Assert.False(File.Exists(store.StoragePath + ".tmp"));
    }

    [Fact]
    public async Task PersistedDocumentContainsEntriesOnlyAndNoCalendarSettings()
    {
        var store = CreateStore();

        await store.SaveAsync(new CalendarState
        {
            Entries = [Entry(new DateOnly(2026, 3, 1), "entry")]
        });
        var json = await File.ReadAllTextAsync(store.StoragePath);

        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"entries\"", json);
        Assert.DoesNotContain("showHolidays", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("defaultView", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("selectedDate", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("search", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NullEntries_NormalizeToIndependentEmptyCollection()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            GetStoragePath(),
            """
            { "schemaVersion": 1, "entries": null }
            """);

        var first = await CreateStore().LoadAsync();
        var second = await CreateStore().LoadAsync();

        Assert.Empty(first.Entries);
        Assert.Empty(second.Entries);
        Assert.NotSame(first.Entries, second.Entries);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task UnsupportedSchema_ReturnsCurrentEmptyState(int schemaVersion)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            GetStoragePath(),
            $$"""
            {
              "schemaVersion": {{schemaVersion}},
              "entries": [{ "id": "{{Guid.NewGuid()}}", "date": "2026-03-01", "text": "ignored" }]
            }
            """);

        var state = await CreateStore().LoadAsync();

        Assert.Equal(CalendarState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Empty(state.Entries);
    }

    [Fact]
    public async Task OlderSchema_NormalizesToCurrentAndPreservesEntries()
    {
        Directory.CreateDirectory(_directory);
        var id = Guid.NewGuid();
        await File.WriteAllTextAsync(
            GetStoragePath(),
            $$"""
            {
              "schemaVersion": 0,
              "entries": [{ "id": "{{id}}", "date": "2026-03-01", "text": "legacy" }]
            }
            """);

        var state = await CreateStore().LoadAsync();

        Assert.Equal(CalendarState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Equal(id, Assert.Single(state.Entries).Id);
    }

    [Fact]
    public void Construction_DoesNotCreateConfiguredOrDefaultUserFile()
    {
        var configured = CreateStore();
        var defaultStore = new JsonCalendarStore();

        Assert.False(File.Exists(configured.StoragePath));
        Assert.Equal(JsonCalendarStore.GetDefaultStoragePath(), defaultStore.StoragePath);
    }

    [Fact]
    public void DefaultPath_IsDedicatedCalendarFileUnderFloatingTools()
    {
        var path = JsonCalendarStore.GetDefaultStoragePath();

        Assert.Equal(JsonCalendarStore.StorageFileName, Path.GetFileName(path));
        Assert.Equal("calendar-entries.json", Path.GetFileName(path));
        Assert.Equal("FloatingTools", Path.GetFileName(Path.GetDirectoryName(path)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private JsonCalendarStore CreateStore() => new(GetStoragePath());

    private string GetStoragePath() => Path.Combine(
        _directory,
        JsonCalendarStore.StorageFileName);

    private static CalendarEntry Entry(DateOnly date, string text) => new()
    {
        Date = date,
        Text = text
    };
}
