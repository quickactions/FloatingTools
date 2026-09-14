using System.Globalization;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class CalendarFocusedCorrectionViewModelTests
{
    private static readonly DateOnly Date = new(2026, 9, 17);

    [Fact]
    public async Task FifthEntryPersistsAndBothAddActionsRejectSixth()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Date);
        for (var index = 1; index <= CalendarToolViewModel.MaximumEntriesPerDate; index++)
        {
            await AddAsync(fixture.ViewModel, $"Event {index}");
        }

        Assert.Equal(5, fixture.Store.Current.Entries.Count(entry => entry.Date == Date));
        Assert.False(fixture.ViewModel.CanAddEvent);
        Assert.False(fixture.ViewModel.BeginAddEventCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.BeginQuickAddCommand.CanExecute(null));

        fixture.ViewModel.BeginAddEventCommand.Execute(null);
        fixture.ViewModel.BeginQuickAddCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsEventEditorOpen);
        Assert.Equal(5, fixture.Store.Current.Entries.Count(entry => entry.Date == Date));
    }

    [Fact]
    public async Task QuickAddWithoutSelectionOnlyShowsTheHintAndCreatesNothing()
    {
        var fixture = await CreateAsync(Enumerable.Range(1, 5)
            .Select(index => Entry($"Existing {index}"))
            .ToArray());

        fixture.ViewModel.BeginQuickAddCommand.Execute(null);

        // Choosing the day is the calendar's job, so nothing is selected,
        // no editor opens and no entry is written.
        Assert.True(fixture.ViewModel.IsAddEventHintVisible);
        Assert.Null(fixture.ViewModel.SelectedDate);
        Assert.False(fixture.ViewModel.IsEventEditorOpen);
        Assert.Equal(5, fixture.Store.Current.Entries.Count);
    }

    [Fact]
    public async Task HolidayDoesNotConsumeUserEntryCapacity()
    {
        var fixture = await CreateAsync(holidayDate: Date);
        fixture.ViewModel.SelectDateCommand.Execute(Date);

        Assert.True(fixture.ViewModel.DayPanel.HasHolidays);
        for (var index = 1; index <= 5; index++)
        {
            await AddAsync(fixture.ViewModel, $"Event {index}");
        }

        Assert.Equal(5, fixture.ViewModel.DayPanel.Entries.Count);
        Assert.True(fixture.ViewModel.DayPanel.HasHolidays);
        Assert.False(fixture.ViewModel.CanAddEvent);
    }

    [Fact]
    public async Task WeekSelectionOnlySelectsAndNeverEntersAddMode()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.ShowWeekCommand.Execute(null);

        fixture.ViewModel.SelectDateCommand.Execute(Date);

        Assert.True(fixture.ViewModel.IsWeekView);
        Assert.Equal(Date, fixture.ViewModel.SelectedDate);
        Assert.True(fixture.ViewModel.DayPanel.HasSelection);
        Assert.False(fixture.ViewModel.IsEventEditorOpen);
    }

    [Fact]
    public async Task ChangingLanguagePreservesEveryOtherSettingAndReselectsChoices()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectedDefaultView = CalendarView.Week;
        fixture.ViewModel.SelectedFirstDayOfWeek = FirstDayOfWeekMode.Monday;
        fixture.ViewModel.ShowHolidays = false;

        fixture.ViewModel.SelectedLanguage = CalendarLanguageMode.Hebrew;

        Assert.Equal(CalendarView.Week, fixture.ViewModel.SelectedDefaultView);
        Assert.Equal(FirstDayOfWeekMode.Monday, fixture.ViewModel.SelectedFirstDayOfWeek);
        Assert.False(fixture.ViewModel.ShowHolidays);
        Assert.Contains(fixture.ViewModel.ViewChoices,
            choice => choice.Value == fixture.ViewModel.SelectedDefaultView);
        Assert.Contains(fixture.ViewModel.FirstDayChoices,
            choice => choice.Value == fixture.ViewModel.SelectedFirstDayOfWeek);
    }

    [Fact]
    public async Task DefaultSettingsAlwaysHaveSelectableNonBlankValues()
    {
        var fixture = await CreateAsync();

        Assert.Equal(CalendarView.Month, fixture.ViewModel.SelectedDefaultView);
        Assert.Equal(FirstDayOfWeekMode.System, fixture.ViewModel.SelectedFirstDayOfWeek);
        Assert.Contains(fixture.ViewModel.ViewChoices, choice =>
            choice.Value == CalendarView.Month && !string.IsNullOrWhiteSpace(choice.DisplayName));
        Assert.Contains(fixture.ViewModel.FirstDayChoices, choice =>
            choice.Value == FirstDayOfWeekMode.System && !string.IsNullOrWhiteSpace(choice.DisplayName));
    }

    [Fact]
    public async Task CollapsingDayPanelPreservesSelectedDateAndPanelSelection()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Date);

        fixture.ViewModel.ToggleDayPanelCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsDayPanelExpanded);
        Assert.Equal(Date, fixture.ViewModel.SelectedDate);
        Assert.True(fixture.ViewModel.DayPanel.HasSelection);
    }

    [Fact]
    public async Task SelectingAnotherDateAndDayPanelAddReexpandThePanel()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Date);
        fixture.ViewModel.ToggleDayPanelCommand.Execute(null);

        fixture.ViewModel.SelectDateCommand.Execute(Date.AddDays(1));

        Assert.True(fixture.ViewModel.IsDayPanelExpanded);
        fixture.ViewModel.ToggleDayPanelCommand.Execute(null);
        fixture.ViewModel.BeginAddEventCommand.Execute(null);
        Assert.True(fixture.ViewModel.IsDayPanelExpanded);
        Assert.True(fixture.ViewModel.IsAddingEvent);
    }

    [Fact]
    public async Task GlobalQuickAddExpandsPanelWhileViewSwitchAloneDoesNot()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.ToggleDayPanelCommand.Execute(null);

        fixture.ViewModel.ShowWeekCommand.Execute(null);
        fixture.ViewModel.ShowMonthCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsDayPanelExpanded);
        fixture.ViewModel.BeginQuickAddCommand.Execute(null);
        Assert.True(fixture.ViewModel.IsDayPanelExpanded);
        Assert.True(fixture.ViewModel.IsAddEventHintVisible);
    }

    [Fact]
    public async Task ToolHeaderCanOpenFromCalendarSettingsPage()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.OpenSettingsCommand.Execute(null);

        fixture.ViewModel.ToggleHeaderCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsSettingsPage);
        Assert.True(fixture.ViewModel.IsHeaderExpanded);
    }

    private static async Task AddAsync(CalendarToolViewModel viewModel, string text)
    {
        viewModel.BeginAddEventCommand.Execute(null);
        viewModel.EventDraftText = text;
        await viewModel.SaveEventCommand.ExecuteAsync(null);
    }

    private static CalendarEntry Entry(string text) => new()
    {
        Id = Guid.NewGuid(),
        Date = Date,
        Text = text
    };

    private static async Task<Fixture> CreateAsync(
        IReadOnlyList<CalendarEntry>? entries = null,
        DateOnly? holidayDate = null)
    {
        var store = new RecordingCalendarStore(entries ?? []);
        var viewModel = new CalendarToolViewModel(
            new CalendarSettings
            {
                Language = CalendarLanguageMode.English,
                FirstDayOfWeek = FirstDayOfWeekMode.System,
                ShowHolidays = true
            },
            new CalendarLanguageResolver(() => new CultureInfo("en-US")),
            new TestHolidayProvider(holidayDate),
            todayProvider: () => Date,
            systemCulture: new CultureInfo("en-US"),
            calendarStore: store);
        await viewModel.InitializeAsync();
        return new Fixture(viewModel, store);
    }

    private sealed record Fixture(
        CalendarToolViewModel ViewModel,
        RecordingCalendarStore Store);

    private sealed class RecordingCalendarStore(IReadOnlyList<CalendarEntry> entries)
        : ICalendarStore
    {
        public CalendarState Current { get; private set; } = new()
        {
            Entries = entries.Select(Clone).ToList()
        };

        public Task<CalendarState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CalendarState
            {
                Entries = Current.Entries.Select(Clone).ToList()
            });

        public Task SaveAsync(CalendarState state, CancellationToken cancellationToken = default)
        {
            Current = new CalendarState
            {
                Entries = state.Entries.Select(Clone).ToList()
            };
            return Task.CompletedTask;
        }

        private static CalendarEntry Clone(CalendarEntry entry) => new()
        {
            Id = entry.Id,
            Date = entry.Date,
            Text = entry.Text
        };
    }

    private sealed class TestHolidayProvider(DateOnly? holidayDate) : IHolidayProvider
    {
        public IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date) =>
            date == holidayDate
                ? [new CalendarHoliday(date, "Test holiday", "חג בדיקה")]
                : [];
    }
}
