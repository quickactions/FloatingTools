using System.Globalization;
using System.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class CalendarStageThreeViewModelTests
{
    private static readonly DateOnly Today = new(2026, 9, 17);

    [Fact]
    public void SupportedRange_IsInclusiveAndExplicit()
    {
        Assert.True(CalendarSupportedDateRange.Contains(new DateOnly(1900, 1, 1)));
        Assert.True(CalendarSupportedDateRange.Contains(new DateOnly(2100, 12, 31)));
        Assert.False(CalendarSupportedDateRange.Contains(new DateOnly(1899, 12, 31)));
        Assert.False(CalendarSupportedDateRange.Contains(new DateOnly(2101, 1, 1)));
    }

    [Fact]
    public async Task MonthNavigation_DisablesAtBothRangeBoundaries()
    {
        var fixture = await CreateAsync(today: CalendarSupportedDateRange.Minimum);
        Assert.False(fixture.ViewModel.NavigatePreviousCommand.CanExecute(null));
        Assert.True(fixture.ViewModel.NavigateNextCommand.CanExecute(null));

        fixture.ViewModel.SearchText = "31.12.2100";
        fixture.ViewModel.SubmitSearchCommand.Execute(null);

        Assert.Equal(CalendarSupportedDateRange.Maximum, fixture.ViewModel.SelectedDate);
        Assert.False(fixture.ViewModel.NavigateNextCommand.CanExecute(null));
        Assert.All(fixture.ViewModel.MonthDays.Where(day => !day.IsSupported),
            day => Assert.True(day.Date > CalendarSupportedDateRange.Maximum));
    }

    [Fact]
    public async Task WeekBoundary_ProducesSevenSupportedDatesAndDisablesNavigation()
    {
        var fixture = await CreateAsync(today: CalendarSupportedDateRange.Maximum);
        fixture.ViewModel.ShowWeekCommand.Execute(null);

        Assert.Equal(7, fixture.ViewModel.WeekDays.Count);
        Assert.All(fixture.ViewModel.WeekDays,
            day => Assert.True(CalendarSupportedDateRange.Contains(day.Date)));
        Assert.Equal(CalendarSupportedDateRange.Maximum, fixture.ViewModel.WeekDays[^1].Date);
        Assert.False(fixture.ViewModel.NavigateNextCommand.CanExecute(null));
    }

    [Fact]
    public async Task OutOfRangeToday_IsUnavailableAndDoesNotSelect()
    {
        var fixture = await CreateAsync(today: new DateOnly(1899, 12, 31));

        Assert.False(fixture.ViewModel.GoToTodayCommand.CanExecute(null));
        fixture.ViewModel.GoToTodayCommand.Execute(null);
        Assert.Null(fixture.ViewModel.SelectedDate);
        Assert.Equal(CalendarSupportedDateRange.Minimum, fixture.ViewModel.DisplayedDate);
    }

    [Fact]
    public async Task ClearSelection_ReturnsDayPanelToDefaultState()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Today);

        fixture.ViewModel.ClearSelectionCommand.Execute(null);

        Assert.Null(fixture.ViewModel.SelectedDate);
        Assert.False(fixture.ViewModel.DayPanel.HasSelection);
        Assert.Equal("Select a day", fixture.ViewModel.DayPanel.EmptyStateText);
    }

    [Fact]
    public async Task ViewSwitch_PreservesSelectionAndPersistsCurrentDefault()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Today);

        fixture.ViewModel.ShowWeekCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsWeekView);
        Assert.Equal(Today, fixture.ViewModel.SelectedDate);
        Assert.Equal(CalendarView.Week, fixture.Settings.Calendar.DefaultView);
        Assert.Equal(1, fixture.SettingsStore.SaveCount);
    }

    [Fact]
    public async Task AddEvent_PersistsBeforeUpdatingAllPresentations()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Today);
        fixture.ViewModel.BeginAddEventCommand.Execute(null);
        fixture.ViewModel.EventDraftText = "Dentist";

        await fixture.ViewModel.SaveEventCommand.ExecuteAsync(null);

        var saved = Assert.Single(fixture.Store.Current.Entries);
        Assert.Equal(Today, saved.Date);
        Assert.Equal("Dentist", saved.Text);
        Assert.Equal("Dentist", Assert.Single(fixture.ViewModel.DayPanel.Entries).Text);
        Assert.Contains(fixture.ViewModel.MonthDays,
            day => day.Date == Today && day.HasEvents);
        Assert.False(fixture.ViewModel.IsEventEditorOpen);
    }

    [Fact]
    public async Task Initialization_LoadsExistingEntriesThroughCalendarStore()
    {
        var entry = Entry(Today, "Loaded event");
        var fixture = await CreateAsync(entries: [entry]);

        fixture.ViewModel.SelectDateCommand.Execute(Today);

        Assert.Equal(entry.Id, Assert.Single(fixture.ViewModel.DayPanel.Entries).Id);
        Assert.Contains(fixture.ViewModel.MonthDays,
            day => day.Date == Today && day.HasEvents);
    }

    [Fact]
    public async Task MultipleEvents_OnOneDateRemainIndependent()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Today);

        await AddAsync(fixture.ViewModel, "First");
        await AddAsync(fixture.ViewModel, "Second");

        Assert.Equal(["First", "Second"],
            fixture.ViewModel.DayPanel.Entries.Select(entry => entry.Text));
        Assert.Equal(2, fixture.Store.Current.Entries.Count);
    }

    [Fact]
    public async Task EditEvent_PersistsInPlaceWithoutChangingIdentity()
    {
        var entry = Entry(Today, "Before");
        var fixture = await CreateAsync(entries: [entry]);
        fixture.ViewModel.SelectDateCommand.Execute(Today);
        fixture.ViewModel.BeginEditEventCommand.Execute(
            Assert.Single(fixture.ViewModel.DayPanel.Entries));
        fixture.ViewModel.EventDraftText = "After";

        await fixture.ViewModel.SaveEventCommand.ExecuteAsync(null);

        var saved = Assert.Single(fixture.Store.Current.Entries);
        Assert.Equal(entry.Id, saved.Id);
        Assert.Equal("After", saved.Text);
        Assert.Equal("After", Assert.Single(fixture.ViewModel.DayPanel.Entries).Text);
    }

    [Fact]
    public async Task DeleteEvent_PersistsAndSynchronizesWeek()
    {
        var entry = Entry(Today, "Remove me");
        var fixture = await CreateAsync(entries: [entry]);
        fixture.ViewModel.SelectDateCommand.Execute(Today);
        fixture.ViewModel.ShowWeekCommand.Execute(null);
        var item = Assert.Single(fixture.ViewModel.DayPanel.Entries);

        await fixture.ViewModel.DeleteEventCommand.ExecuteAsync(item);

        Assert.Empty(fixture.Store.Current.Entries);
        Assert.Empty(fixture.ViewModel.DayPanel.Entries);
        Assert.Empty(fixture.ViewModel.WeekDays.Single(day => day.Date == Today).Entries);
    }

    [Fact]
    public async Task CopyEvent_UsesExistingClipboardService()
    {
        var entry = Entry(Today, "Copy this");
        var fixture = await CreateAsync(entries: [entry]);
        fixture.ViewModel.SelectDateCommand.Execute(Today);

        fixture.ViewModel.CopyEventCommand.Execute(
            Assert.Single(fixture.ViewModel.DayPanel.Entries));

        Assert.Equal("Copy this", fixture.Clipboard.Text);
    }

    [Fact]
    public async Task EventPresentation_UsesFirstStrongDirectionWithVerifiedWpfMapping()
    {
        var fixture = await CreateAsync(entries:
        [
            Entry(Today, "שלום world"),
            Entry(Today, "hello שלום")
        ]);
        fixture.ViewModel.SelectDateCommand.Execute(Today);

        var hebrew = fixture.ViewModel.DayPanel.Entries[0];
        var english = fixture.ViewModel.DayPanel.Entries[1];
        Assert.Equal(FlowDirection.RightToLeft, hebrew.FlowDirection);
        Assert.Equal(TextAlignment.Left, hebrew.TextAlignment);
        Assert.Equal(FlowDirection.LeftToRight, english.FlowDirection);
        Assert.Equal(TextAlignment.Left, english.TextAlignment);
    }

    [Fact]
    public async Task PersistenceFailure_DoesNotPresentUnsavedAddAsSuccessful()
    {
        var fixture = await CreateAsync();
        fixture.Store.FailSave = true;
        fixture.ViewModel.SelectDateCommand.Execute(Today);
        fixture.ViewModel.BeginAddEventCommand.Execute(null);
        fixture.ViewModel.EventDraftText = "Unsaved";

        await fixture.ViewModel.SaveEventCommand.ExecuteAsync(null);

        Assert.Empty(fixture.Store.Current.Entries);
        Assert.Empty(fixture.ViewModel.DayPanel.Entries);
        Assert.True(fixture.ViewModel.IsEventEditorOpen);
        Assert.Equal("Unsaved", fixture.ViewModel.EventDraftText);
        Assert.NotNull(fixture.ViewModel.OperationErrorMessage);
    }

    [Fact]
    public async Task QuickAdd_WithSelectionOpensSingleDayPanelEditor()
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.SelectDateCommand.Execute(Today);

        fixture.ViewModel.BeginQuickAddCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsAddingEvent);
        Assert.False(fixture.ViewModel.IsAddEventHintVisible);
        Assert.Equal(Today, fixture.ViewModel.SelectedDate);
    }

    [Fact]
    public async Task QuickAdd_WithoutSelectionShowsHintThenSelectingADayClearsIt()
    {
        var fixture = await CreateAsync();

        fixture.ViewModel.BeginQuickAddCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsAddEventHintVisible);
        Assert.Null(fixture.ViewModel.SelectedDate);
        Assert.False(fixture.ViewModel.IsAddingEvent);

        fixture.ViewModel.SelectDateCommand.Execute(Today);

        Assert.False(fixture.ViewModel.IsAddEventHintVisible);
        Assert.Equal(Today, fixture.ViewModel.SelectedDate);
    }

    [Theory]
    [InlineData("1.3.26")]
    [InlineData("01/03/2026")]
    [InlineData("1-3-26")]
    [InlineData("1 3 26")]
    public async Task DateSearch_NavigatesAndSelectsWithoutRewritingInput(string input)
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.IsHeaderExpanded = true;
        fixture.ViewModel.SearchText = input;

        fixture.ViewModel.SubmitSearchCommand.Execute(null);

        Assert.Equal(input, fixture.ViewModel.SearchText);
        Assert.Equal(new DateOnly(2026, 3, 1), fixture.ViewModel.SelectedDate);
        Assert.False(fixture.ViewModel.IsHeaderExpanded);
    }

    [Theory]
    [InlineData("31.2.2026")]
    [InlineData("31.12.2101")]
    public async Task InvalidOrOutOfRangeDateSearch_StaysOpenAndDoesNotNavigate(string input)
    {
        var fixture = await CreateAsync();
        fixture.ViewModel.IsHeaderExpanded = true;
        fixture.ViewModel.SearchText = input;

        fixture.ViewModel.SubmitSearchCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsHeaderExpanded);
        Assert.Null(fixture.ViewModel.SelectedDate);
        Assert.NotNull(fixture.ViewModel.SearchValidationMessage);
    }

    [Fact]
    public async Task EventSearch_SelectsDateHighlightsResultAndExcludesHolidays()
    {
        var matching = Entry(new DateOnly(2026, 10, 2), "Project review");
        var fixture = await CreateAsync(entries: [matching], holidayDate: Today);
        fixture.ViewModel.SearchText = "review";
        var result = Assert.Single(fixture.ViewModel.SearchResults);
        Guid? visibilityRequest = null;
        fixture.ViewModel.EntryVisibilityRequested += id => visibilityRequest = id;

        fixture.ViewModel.SelectSearchResultCommand.Execute(result);

        Assert.Equal(matching.Date, fixture.ViewModel.SelectedDate);
        Assert.True(Assert.Single(fixture.ViewModel.DayPanel.Entries).IsSearchTarget);
        Assert.Equal(matching.Id, visibilityRequest);

        fixture.ViewModel.SearchText = "Test Holiday";
        Assert.Empty(fixture.ViewModel.SearchResults);
    }

    [Fact]
    public async Task SettingsPersistLanguageFirstDayHolidayAndDefaultView()
    {
        var fixture = await CreateAsync(holidayDate: Today);
        fixture.ViewModel.SelectDateCommand.Execute(Today);
        Assert.True(fixture.ViewModel.DayPanel.HasHolidays);

        fixture.ViewModel.SelectedLanguage = CalendarLanguageMode.Hebrew;
        fixture.ViewModel.SelectedFirstDayOfWeek = FirstDayOfWeekMode.Monday;
        fixture.ViewModel.ShowHolidays = false;
        fixture.ViewModel.SelectedDefaultView = CalendarView.Week;

        Assert.Equal(CalendarDisplayLanguage.Hebrew, fixture.ViewModel.DisplayLanguage);
        Assert.Equal(DayOfWeek.Monday, fixture.ViewModel.FirstDayOfWeek);
        Assert.False(fixture.ViewModel.DayPanel.HasHolidays);
        Assert.True(fixture.ViewModel.IsWeekView);
        Assert.Equal(4, fixture.SettingsStore.SaveCount);
        Assert.Equal(CalendarLanguageMode.Hebrew, fixture.Settings.Calendar.Language);
        Assert.False(fixture.Settings.Calendar.ShowHolidays);
    }

    [Fact]
    public async Task SettingsFailure_RollsBackVisibleSetting()
    {
        var fixture = await CreateAsync();
        fixture.SettingsStore.FailSave = true;

        fixture.ViewModel.SelectedFirstDayOfWeek = FirstDayOfWeekMode.Monday;

        Assert.Equal(FirstDayOfWeekMode.Sunday, fixture.ViewModel.SelectedFirstDayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, fixture.ViewModel.FirstDayOfWeek);
        Assert.NotNull(fixture.ViewModel.SettingsErrorMessage);
    }

    [Fact]
    public async Task HolidayOnlyAndEventDaysUseCorrectNoEventsSemantics()
    {
        var fixture = await CreateAsync(entries: [Entry(Today.AddDays(1), "Event")], holidayDate: Today);
        fixture.ViewModel.ShowWeekCommand.Execute(null);

        var holiday = fixture.ViewModel.WeekDays.Single(day => day.Date == Today);
        var eventDay = fixture.ViewModel.WeekDays.Single(day => day.Date == Today.AddDays(1));
        var empty = fixture.ViewModel.WeekDays.First(day => day.Date != Today && day.Date != Today.AddDays(1));
        Assert.False(holiday.ShowNoEvents);
        Assert.False(eventDay.ShowNoEvents);
        Assert.True(empty.ShowNoEvents);
    }

    private static async Task AddAsync(CalendarToolViewModel viewModel, string text)
    {
        viewModel.BeginAddEventCommand.Execute(null);
        viewModel.EventDraftText = text;
        await viewModel.SaveEventCommand.ExecuteAsync(null);
    }

    private static CalendarEntry Entry(DateOnly date, string text) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        Text = text
    };

    private static async Task<Fixture> CreateAsync(
        DateOnly? today = null,
        IReadOnlyList<CalendarEntry>? entries = null,
        DateOnly? holidayDate = null)
    {
        var settings = new AppSettings
        {
            Calendar = new CalendarSettings
            {
                Language = CalendarLanguageMode.English,
                DefaultView = CalendarView.Month,
                FirstDayOfWeek = FirstDayOfWeekMode.Sunday,
                ShowHolidays = true
            }
        };
        var settingsStore = new RecordingSettingsStore();
        var store = new RecordingCalendarStore(entries ?? []);
        var clipboard = new RecordingClipboard();
        var viewModel = new CalendarToolViewModel(
            settings,
            settingsStore,
            new CalendarLanguageResolver(() => new CultureInfo("en-US")),
            new TestHolidayProvider(holidayDate),
            store,
            clipboard,
            todayProvider: () => today ?? Today,
            systemCulture: new CultureInfo("en-US"));
        await viewModel.InitializeAsync();
        return new Fixture(viewModel, store, clipboard, settings, settingsStore);
    }

    private sealed record Fixture(
        CalendarToolViewModel ViewModel,
        RecordingCalendarStore Store,
        RecordingClipboard Clipboard,
        AppSettings Settings,
        RecordingSettingsStore SettingsStore);

    private sealed class RecordingCalendarStore(IReadOnlyList<CalendarEntry> entries)
        : ICalendarStore
    {
        public CalendarState Current { get; private set; } = new()
        {
            Entries = entries.Select(Clone).ToList()
        };

        public bool FailSave { get; set; }

        public Task<CalendarState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CalendarState
            {
                Entries = Current.Entries.Select(Clone).ToList()
            });

        public Task SaveAsync(CalendarState state, CancellationToken cancellationToken = default)
        {
            if (FailSave)
            {
                throw new IOException("Simulated failure");
            }

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

    private sealed class RecordingSettingsStore : IAppSettingsStore
    {
        public int SaveCount { get; private set; }

        public bool FailSave { get; set; }

        public AppSettings Load() => new();

        public bool Save(AppSettings settings)
        {
            SaveCount++;
            return !FailSave;
        }
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; private set; }

        public void SetText(string text) => Text = text;
    }

    private sealed class TestHolidayProvider(DateOnly? holidayDate) : IHolidayProvider
    {
        public IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date) =>
            date == holidayDate
                ? [new CalendarHoliday(date, "Test Holiday", "חג בדיקה")]
                : [];
    }
}
