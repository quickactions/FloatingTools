using System.Globalization;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class CalendarEventsStageTwoTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);
    internal static CalendarToolViewModel CreateCalendar(CalendarLanguageMode language = CalendarLanguageMode.English)
        => new(new CalendarSettings { Language = language }, new CalendarLanguageResolver(),
            new HebrewCalendarHolidayProvider(), todayProvider: () => Today, systemCulture: new CultureInfo("en-US"));

    [Fact]
    public async Task ReopeningRefreshesSavedEntriesWithoutResettingSession()
    {
        var calendar = CreateCalendar();
        calendar.OpenEventsCommand.Execute(null);
        var events = calendar.EventsViewModel!;
        Assert.Equal(CalendarEventsPreset.All, events.SelectedPreset);
        events.SelectedPreset = CalendarEventsPreset.FromToday;
        events.SearchText = "New";
        events.ToggleSortCommand.Execute(null);
        calendar.BackToCalendarCommand.Execute(null);
        calendar.SelectDateCommand.Execute(Today);
        calendar.BeginAddEventCommand.Execute(null);
        calendar.EventDraftText = "New event";
        await calendar.SaveEventCommand.ExecuteAsync(null);
        calendar.OpenEventsCommand.Execute(null);
        Assert.Same(events, calendar.EventsViewModel);
        Assert.Equal(CalendarEventsPreset.FromToday, events.SelectedPreset);
        Assert.Equal("New", events.SearchText);
        Assert.True(events.IsDescending);
        Assert.Equal("New event", Assert.Single(events.Items).Text);
    }
}
