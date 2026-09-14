using FloatingTools.App.Models;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class CalendarEventsSearchAndPresetTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);

    [Theory]
    [InlineData(CalendarEventsPreset.All, 5)]
    [InlineData(CalendarEventsPreset.FromToday, 4)]
    [InlineData(CalendarEventsPreset.CurrentWeek, 3)]
    [InlineData(CalendarEventsPreset.CurrentMonth, 4)]
    public void FiltersRefreshImmediatelyAndUnboundedFiltersHaveNoOneYearLimit(CalendarEventsPreset preset, int count)
    {
        var vm = Create();
        vm.SelectedPreset = preset;
        Assert.Equal(count, vm.Items.Count);
        if (preset is CalendarEventsPreset.All or CalendarEventsPreset.FromToday)
            Assert.Contains(vm.Items, x => x.Date == Today.AddYears(5));
    }

    [Theory]
    [InlineData(FirstDayOfWeekMode.Sunday, 6, 12)]
    [InlineData(FirstDayOfWeekMode.Monday, 7, 13)]
    public void CurrentWeekUsesCalendarSettings(FirstDayOfWeekMode firstDay, int start, int end)
    {
        var calendar = CalendarEventsStageTwoTests.CreateCalendar();
        calendar.SelectedFirstDayOfWeek = firstDay;
        calendar.OpenEventsCommand.Execute(null);
        calendar.EventsViewModel!.SelectedPreset = CalendarEventsPreset.CurrentWeek;
        Assert.Equal(new DateOnly(2026, 9, start), calendar.EventsViewModel.RangeStart);
        Assert.Equal(new DateOnly(2026, 9, end), calendar.EventsViewModel.RangeEnd);
    }

    [Theory]
    [InlineData("HELLO")]
    [InlineData("שלום")]
    public void SearchIsLiveAndComposesWithPresetAndStableSort(string query)
    {
        var vm = Create();
        vm.SelectedPreset = CalendarEventsPreset.FromToday;
        vm.SearchText = query;
        Assert.Equal("Hello שלום", Assert.Single(vm.Items).Text);
        vm.SelectedPreset = CalendarEventsPreset.All;
        Assert.Equal(2, vm.Items.Count);
        vm.SearchText = "";
        vm.ToggleSortCommand.Execute(null);
        Assert.Equal(["future", "next", "Hello שלום", "second", "Hello שלום before"],
            vm.Items.Select(x => x.Text));
    }

    [Fact]
    public void CurrentMonthIncludesBothBoundaries()
    {
        var vm = Create();
        vm.SelectedPreset = CalendarEventsPreset.CurrentMonth;
        Assert.Equal(new DateOnly(2026, 9, 1), vm.RangeStart);
        Assert.Equal(new DateOnly(2026, 9, 30), vm.RangeEnd);
    }

    [Fact]
    public void OptionsAreLocalizedAndContainOnlyFourPresets()
    {
        var vm = Create();
        Assert.Equal(["All", "From today", "Current week", "Current month"], vm.PresetChoices.Select(x => x.DisplayName));
        vm.RefreshSource([], true);
        Assert.Equal("סינון לפי", vm.FilterLabel);
        Assert.Equal(["הכל", "מהיום", "השבוע הנוכחי", "החודש הנוכחי"], vm.PresetChoices.Select(x => x.DisplayName));
    }

    [Fact]
    public void ContextualResultsIgnoreFullPageSearchAndPreset()
    {
        var vm = new CalendarEventsViewModel(CalendarEventsMode.Contextual, Today, Today,
            [new CalendarEntry { Date = Today, Text = "entry" }], false, (_, _) => { });
        vm.SearchText = "no match";
        vm.SelectedPreset = CalendarEventsPreset.CurrentMonth;
        vm.ToggleSortCommand.Execute(null);
        Assert.Equal("entry", Assert.Single(vm.Items).Text);
        Assert.Equal(Today, vm.RangeStart);
        Assert.Equal(Today, vm.RangeEnd);
    }

    private static CalendarEventsViewModel Create() => new(CalendarEventsMode.Full, Today, Today.AddYears(1),
        [
            new CalendarEntry { Date = Today.AddDays(-7), Text = "Hello שלום before" },
            new CalendarEntry { Date = Today, Text = "Hello שלום" },
            new CalendarEntry { Date = Today, Text = "second" },
            new CalendarEntry { Date = Today.AddDays(1), Text = "next" },
            new CalendarEntry { Date = Today.AddYears(5), Text = "future" }
        ], false, (_, _) => { }, todayProvider: () => Today,
        weekStartProvider: date => date.AddDays(-1));
}
