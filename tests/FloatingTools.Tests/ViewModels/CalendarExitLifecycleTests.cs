using System.Globalization;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class CalendarExitLifecycleTests
{
    [Fact]
    public async Task PrepareForExitAsync_WaitsForAnInFlightImmediatePersistenceMutation()
    {
        var today = new DateOnly(2026, 9, 1);
        var store = new BlockingCalendarStore();
        var settings = new AppSettings
        {
            Calendar = new CalendarSettings
            {
                Language = CalendarLanguageMode.English,
                DefaultView = CalendarView.Month,
                FirstDayOfWeek = FirstDayOfWeekMode.Sunday
            }
        };
        var viewModel = new CalendarToolViewModel(
            settings,
            new InMemoryAppSettingsStore(settings),
            new CalendarLanguageResolver(() => new CultureInfo("en-US")),
            new NoHolidays(),
            store,
            new NullClipboardService(),
            todayProvider: () => today,
            systemCulture: new CultureInfo("en-US"));
        await viewModel.InitializeAsync();
        viewModel.SelectDateCommand.Execute(today);
        viewModel.BeginAddEventCommand.Execute(null);
        viewModel.EventDraftText = "Persist before exit";

        var save = viewModel.SaveEventCommand.ExecuteAsync(null);
        await store.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var prepareForExit = viewModel.PrepareForExitAsync();

        Assert.False(prepareForExit.IsCompleted);

        store.AllowSave.SetResult();
        await Task.WhenAll(save, prepareForExit);

        Assert.Equal("Persist before exit", Assert.Single(store.State.Entries).Text);
    }

    private sealed class BlockingCalendarStore : ICalendarStore
    {
        public TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowSave { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CalendarState State { get; private set; } = new();

        public Task<CalendarState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CalendarState());

        public async Task SaveAsync(
            CalendarState state,
            CancellationToken cancellationToken = default)
        {
            SaveStarted.TrySetResult();
            await AllowSave.Task.WaitAsync(cancellationToken);
            State = new CalendarState
            {
                Entries = state.Entries.Select(entry => new CalendarEntry
                {
                    Id = entry.Id,
                    Date = entry.Date,
                    Text = entry.Text
                }).ToList()
            };
        }
    }

    private sealed class NoHolidays : IHolidayProvider
    {
        public IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date) => [];
    }
}
