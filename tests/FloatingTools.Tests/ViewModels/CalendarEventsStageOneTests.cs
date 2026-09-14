using System.Globalization;
using System.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class CalendarEventsStageOneTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);

    [Fact]
    public async Task CalendarPageFlagsAreMutuallyExclusive()
    {
        var viewModel = await CreateAsync();

        foreach (var page in Enum.GetValues<CalendarPage>())
        {
            viewModel.CurrentPage = page;
            Assert.Equal(page == CalendarPage.Calendar, viewModel.IsCalendarPage);
            Assert.Equal(page == CalendarPage.Events, viewModel.IsEventsPage);
            Assert.Equal(page == CalendarPage.Settings, viewModel.IsSettingsPage);
        }
    }

    [Fact]
    public async Task ContextualMonthNavigationUpdatesRangeAndEntriesInPlace()
    {
        var inside = Entry(new DateOnly(2026, 9, 30), "inside");
        var outside = Entry(new DateOnly(2026, 10, 1), "outside");
        var viewModel = await CreateAsync([inside, outside]);

        viewModel.OpenContextualEventsCommand.Execute(null);

        var contextual = viewModel.ContextualEventsViewModel!;
        Assert.Equal(new DateOnly(2026, 9, 1), contextual.RangeStart);
        Assert.Equal(new DateOnly(2026, 9, 30), contextual.RangeEnd);
        Assert.Equal(inside.Id, Assert.Single(contextual.Items).Id);
        viewModel.NavigateNextCommand.Execute(null);
        Assert.Same(contextual, viewModel.ContextualEventsViewModel);
        Assert.Equal(new DateOnly(2026, 10, 1), contextual.RangeStart);
        Assert.Equal(new DateOnly(2026, 10, 31), contextual.RangeEnd);
        Assert.Equal(outside.Id, Assert.Single(contextual.Items).Id);
    }

    [Fact]
    public async Task ContextualWeekNavigationUpdatesRangeAndEntriesInPlace()
    {
        var current = Entry(new DateOnly(2026, 9, 8), "current");
        var next = Entry(new DateOnly(2026, 9, 15), "next");
        var viewModel = await CreateAsync([current, next]);
        viewModel.ShowWeekCommand.Execute(null);

        viewModel.OpenContextualEventsCommand.Execute(null);

        var contextual = viewModel.ContextualEventsViewModel!;
        Assert.Equal(new DateOnly(2026, 9, 6), contextual.RangeStart);
        Assert.Equal(new DateOnly(2026, 9, 12), contextual.RangeEnd);
        Assert.Equal(current.Id, Assert.Single(contextual.Items).Id);
        viewModel.NavigateNextCommand.Execute(null);
        Assert.Same(contextual, viewModel.ContextualEventsViewModel);
        Assert.Equal(new DateOnly(2026, 9, 13), contextual.RangeStart);
        Assert.Equal(new DateOnly(2026, 9, 19), contextual.RangeEnd);
        Assert.Equal(next.Id, Assert.Single(contextual.Items).Id);
    }

    [Theory]
    [InlineData(CalendarLanguageMode.English, false, "Events — September 2026")]
    [InlineData(CalendarLanguageMode.Hebrew, false, "אירועים — ספטמבר 2026")]
    [InlineData(CalendarLanguageMode.English, true, "Events — Sep 6 – 12")]
    [InlineData(CalendarLanguageMode.Hebrew, true, "אירועים — 6–12 בספט׳")]
    public async Task ContextualTitleUsesLocalizedHeadingAndExistingPeriodTitle(
        CalendarLanguageMode language,
        bool week,
        string expected)
    {
        var viewModel = await CreateAsync(language: language);
        if (week) viewModel.ShowWeekCommand.Execute(null);
        viewModel.OpenContextualEventsCommand.Execute(null);

        Assert.Equal(expected, viewModel.ContextualEventsTitle);
    }

    [Fact]
    public async Task ContextualTitleUpdatesOnNavigationLanguageAndViewChanges()
    {
        var viewModel = await CreateAsync();
        viewModel.OpenContextualEventsCommand.Execute(null);
        var contextual = viewModel.ContextualEventsViewModel;

        viewModel.NavigateNextCommand.Execute(null);
        Assert.Equal("Events — October 2026", viewModel.ContextualEventsTitle);

        viewModel.ShowWeekCommand.Execute(null);
        Assert.Same(contextual, viewModel.ContextualEventsViewModel);
        Assert.Equal(viewModel.VisibleRangeStart, contextual!.RangeStart);
        Assert.Equal(viewModel.VisibleRangeEnd, contextual.RangeEnd);
        Assert.Equal($"Events — {viewModel.PeriodTitle}", viewModel.ContextualEventsTitle);

        viewModel.SelectedLanguage = CalendarLanguageMode.Hebrew;
        Assert.Same(contextual, viewModel.ContextualEventsViewModel);
        Assert.Equal($"אירועים — {viewModel.PeriodTitle}", viewModel.ContextualEventsTitle);
    }

    [Fact]
    public void EventItemExpansionAndTextDirectionAreDerivedFromOriginalText()
    {
        var hebrew = new CalendarEventListItemViewModel(
            Entry(Today, "3 דברים חשובים למחר"), new("3 דברים חשובים…", true), "08/09/2026");
        var english = new CalendarEventListItemViewModel(
            Entry(Today, "2026 Goals"), new("2026 Goals", false), "08/09/2026");

        Assert.True(hebrew.IsCollapsed);
        Assert.False(hebrew.IsExpanded);
        Assert.True(hebrew.HasHiddenContent);
        Assert.True(hebrew.ToggleExpandedCommand.CanExecute(null));
        hebrew.ToggleExpandedCommand.Execute(null);
        Assert.False(hebrew.IsCollapsed);
        Assert.True(hebrew.IsExpanded);
        Assert.False(english.HasHiddenContent);
        Assert.False(english.ToggleExpandedCommand.CanExecute(null));
        english.ToggleExpandedCommand.Execute(null);
        Assert.False(english.IsExpanded);
        Assert.Equal(FlowDirection.RightToLeft, hebrew.TextFlowDirection);
        Assert.Equal(TextAlignment.Left, hebrew.TextAlignment);
        Assert.Equal(FlowDirection.LeftToRight, english.TextFlowDirection);
        Assert.Equal(TextAlignment.Left, english.TextAlignment);
    }

    [Fact]
    public void EventsViewModelSortCommandReordersDatesButKeepsSameDateOrder()
    {
        var early = Today;
        var late = Today.AddDays(1);
        var entries = new[]
        {
            Entry(late, "late one"), Entry(early, "early one"),
            Entry(late, "late two"), Entry(early, "early two")
        };
        var events = new CalendarEventsViewModel(
            CalendarEventsMode.Contextual, early, late, entries,
            isHebrew: false, (_, _) => { });

        Assert.Equal(["early one", "early two", "late one", "late two"],
            events.Items.Select(item => item.Text));

        events.ToggleSortCommand.Execute(null);

        Assert.Equal(["late one", "late two", "early one", "early two"],
            events.Items.Select(item => item.Text));
    }

    [Fact]
    public void ContextualRangeRefreshPreservesSortAndExpandedRowsThatRemainInRange()
    {
        var entry = Entry(Today, "one two three four");
        var events = new CalendarEventsViewModel(
            CalendarEventsMode.Contextual, Today.AddDays(-7), Today.AddDays(7), [entry],
            isHebrew: false, (_, _) => { });
        events.ToggleSortCommand.Execute(null);
        Assert.Single(events.Items).ToggleExpandedCommand.Execute(null);

        events.SetRange(Today, Today.AddDays(1));

        Assert.True(events.IsDescending);
        Assert.True(Assert.Single(events.Items).IsExpanded);
    }

    [Theory]
    [InlineData(CalendarEventsMode.Contextual, CalendarLayoutMode.Compact, "one two three…", true)]
    [InlineData(CalendarEventsMode.Full, CalendarLayoutMode.Compact, "one two three…", true)]
    [InlineData(CalendarEventsMode.Contextual, CalendarLayoutMode.Large, "one two three four five six", false)]
    [InlineData(CalendarEventsMode.Full, CalendarLayoutMode.Large, "one two three four five six", false)]
    public void LayoutModeSelectsPreviewIndependentlyOfEventsMode(
        CalendarEventsMode eventsMode,
        CalendarLayoutMode layoutMode,
        string expectedPreview,
        bool expectedHiddenContent)
    {
        var entry = Entry(Today, "one two three four five six");
        var events = new CalendarEventsViewModel(
            eventsMode, Today, Today, [entry], isHebrew: false, (_, _) => { },
            todayProvider: () => Today, layoutMode: layoutMode);
        var item = Assert.Single(events.Items);

        Assert.Equal(expectedPreview, item.Preview);
        Assert.Equal(expectedHiddenContent, item.HasHiddenContent);
    }

    [Fact]
    public async Task LayoutModeChangeRefreshesBothExistingListsAndPreservesTheirState()
    {
        var entry = Entry(Today, "one two three four five six seven eight");
        var owner = await CreateAsync([entry]);
        owner.OpenContextualEventsCommand.Execute(null);
        var contextual = owner.ContextualEventsViewModel!;
        contextual.ToggleSortCommand.Execute(null);
        Assert.Single(contextual.Items).ToggleExpandedCommand.Execute(null);
        owner.OpenEventsCommand.Execute(null);
        var full = owner.EventsViewModel!;
        full.SearchText = "one";
        full.ToggleSortCommand.Execute(null);
        Assert.Single(full.Items).ToggleExpandedCommand.Execute(null);

        owner.SetLayoutMode(CalendarLayoutMode.Large);

        Assert.Equal("one two three four five six seven…", Assert.Single(contextual.Items).Preview);
        Assert.True(Assert.Single(contextual.Items).IsExpanded);
        Assert.Equal("one two three four five six seven…", Assert.Single(full.Items).Preview);
        Assert.True(Assert.Single(full.Items).IsExpanded);
        Assert.True(contextual.IsDescending);
        Assert.True(full.IsDescending);
        Assert.Equal("one", full.SearchText);

        owner.SetLayoutMode(CalendarLayoutMode.Compact);

        Assert.Equal("one two three…", Assert.Single(contextual.Items).Preview);
        Assert.Equal("one two three…", Assert.Single(full.Items).Preview);
        Assert.True(Assert.Single(contextual.Items).IsExpanded);
        Assert.True(Assert.Single(full.Items).IsExpanded);
    }

    [Fact]
    public async Task ActivatingContextualEventReturnsToCalendarAndUsesExactEntryVisibilityPath()
    {
        var entry = Entry(Today.AddDays(1), "Open me");
        var viewModel = await CreateAsync([entry]);
        Guid? requested = null;
        viewModel.EntryVisibilityRequested += id => requested = id;
        viewModel.OpenContextualEventsCommand.Execute(null);

        viewModel.ContextualEventsViewModel!.ActivateEventCommand.Execute(
            Assert.Single(viewModel.ContextualEventsViewModel.Items));

        Assert.True(viewModel.IsCalendarPage);
        Assert.Equal(entry.Date, viewModel.DisplayedDate);
        Assert.Equal(entry.Date, viewModel.SelectedDate);
        Assert.True(viewModel.IsDayPanelExpanded);
        Assert.Equal(entry.Id, viewModel.HighlightedEntryId);
        Assert.True(Assert.Single(viewModel.DayPanel.Entries).IsSearchTarget);
        Assert.Equal(entry.Id, requested);
    }

    [Fact]
    public async Task BackFromEventsPreservesCalendarState()
    {
        var selected = Today.AddDays(2);
        var viewModel = await CreateAsync([Entry(selected, "State")]);
        viewModel.ShowWeekCommand.Execute(null);
        viewModel.SelectDateCommand.Execute(selected);
        viewModel.ToggleDayPanelCommand.Execute(null);
        var displayed = viewModel.DisplayedDate;

        viewModel.OpenContextualEventsCommand.Execute(null);
        viewModel.CloseContextualEventsCommand.Execute(null);

        Assert.True(viewModel.IsCalendarPage);
        Assert.True(viewModel.IsWeekView);
        Assert.Equal(displayed, viewModel.DisplayedDate);
        Assert.Equal(selected, viewModel.SelectedDate);
        Assert.False(viewModel.IsDayPanelExpanded);
        Assert.Equal("State", Assert.Single(viewModel.DayPanel.Entries).Text);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ContextualClosePreservesSelectionExpansionAndDayPanelInstance(bool selected, bool expanded)
    {
        var vm = await CreateAsync();
        if (selected) vm.SelectDateCommand.Execute(Today);
        vm.IsDayPanelExpanded = expanded;
        var day = vm.DayPanel;
        var displayed = vm.DisplayedDate;
        vm.OpenContextualEventsCommand.Execute(null);
        vm.OpenContextualEventsCommand.Execute(null);
        Assert.Equal(CalendarPage.Calendar, vm.CurrentPage);
        Assert.False(vm.IsEventsPage);
        Assert.True(vm.IsCompactPanelExpanded);
        Assert.False(vm.IsDayContentVisible);
        vm.CloseContextualEventsCommand.Execute(null);
        Assert.Equal(expanded, vm.IsDayPanelExpanded);
        Assert.Equal(expanded, vm.IsCompactPanelExpanded);
        Assert.Equal(selected ? Today : (DateOnly?)null, vm.SelectedDate);
        Assert.Equal(displayed, vm.DisplayedDate);
        Assert.Same(day, vm.DayPanel);
    }

    [Fact]
    public async Task MenuEventsUsesFullPageAndSeparateAllEventsList()
    {
        var vm = await CreateAsync([Entry(Today, "month"), Entry(Today.AddMonths(2), "later")]);
        vm.OpenContextualEventsCommand.Execute(null);
        var contextual = vm.ContextualEventsViewModel;
        vm.OpenEventsCommand.Execute(null);
        Assert.Equal(CalendarPage.Events, vm.CurrentPage);
        Assert.Equal(CalendarEventsMode.Full, vm.EventsViewModel!.Mode);
        Assert.Equal(2, vm.EventsViewModel.Items.Count);
        Assert.Single(contextual!.Items);
        Assert.NotSame(contextual, vm.EventsViewModel);
        vm.BackToCalendarCommand.Execute(null);
        Assert.Same(contextual, vm.ContextualEventsViewModel);
        Assert.True(vm.IsContextualEventsOpen);
        Assert.Equal("↑", contextual.SortGlyph);
        contextual.ToggleSortCommand.Execute(null);
        Assert.Equal("↓", contextual.SortGlyph);
    }

    private static async Task<CalendarToolViewModel> CreateAsync(
        IReadOnlyList<CalendarEntry>? entries = null,
        CalendarLanguageMode language = CalendarLanguageMode.English)
    {
        var viewModel = new CalendarToolViewModel(
            new CalendarSettings
            {
                Language = language,
                DefaultView = CalendarView.Month,
                FirstDayOfWeek = FirstDayOfWeekMode.Sunday
            },
            new CalendarLanguageResolver(() => new CultureInfo("en-US")),
            new EmptyHolidayProvider(),
            todayProvider: () => Today,
            systemCulture: new CultureInfo("en-US"),
            calendarStore: new CalendarStore(entries ?? []));
        await viewModel.InitializeAsync();
        return viewModel;
    }

    private static CalendarEntry Entry(DateOnly date, string text) => new()
    {
        Id = Guid.NewGuid(), Date = date, Text = text
    };

    private sealed class EmptyHolidayProvider : IHolidayProvider
    {
        public IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date) => [];
    }

    private sealed class CalendarStore(IReadOnlyList<CalendarEntry> entries) : ICalendarStore
    {
        public Task<CalendarState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CalendarState { Entries = entries.ToList() });

        public Task SaveAsync(CalendarState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
