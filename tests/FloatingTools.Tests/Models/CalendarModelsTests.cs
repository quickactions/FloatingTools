using FloatingTools.App.Models;

namespace FloatingTools.Tests.Models;

public sealed class CalendarModelsTests
{
    [Fact]
    public void EntriesAndStates_CreateIndependentInstances()
    {
        var firstEntry = new CalendarEntry();
        var secondEntry = new CalendarEntry();
        var firstState = new CalendarState();
        var secondState = new CalendarState();

        Assert.NotEqual(firstEntry.Id, secondEntry.Id);
        Assert.NotSame(firstState.Entries, secondState.Entries);
        firstState.Entries.Add(firstEntry);
        Assert.Empty(secondState.Entries);
    }

    [Fact]
    public void CalendarSettings_DefaultToApprovedValues()
    {
        var settings = new CalendarSettings();

        Assert.Equal(CalendarLanguageMode.UseAppLanguage, settings.Language);
        Assert.Equal(CalendarView.Month, settings.DefaultView);
        Assert.Equal(FirstDayOfWeekMode.System, settings.FirstDayOfWeek);
        Assert.True(settings.ShowHolidays);
    }
}
