using System.Globalization;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class CalendarToolViewModelTests
{
    [Fact]
    public void MonthGrid_UsesFiveRowsWhenCurrentMonthFitsAndCompletesBoundaryWeeks()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));

        Assert.Equal(35, viewModel.MonthDays.Count);
        Assert.Equal(5, viewModel.MonthWeekRowCount);
        Assert.Equal(new DateOnly(2026, 8, 30), viewModel.MonthDays[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 3), viewModel.MonthDays[^1].Date);
        Assert.False(viewModel.MonthDays[0].IsCurrentMonth);
        Assert.False(viewModel.MonthDays[^1].IsCurrentMonth);
        Assert.Equal(DayOfWeek.Sunday, viewModel.MonthDays[0].Date.DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, viewModel.MonthDays[^1].Date.DayOfWeek);
    }

    [Fact]
    public void MonthGrid_UsesSixRowsWhenCurrentMonthRequiresThem()
    {
        var viewModel = Create(new DateOnly(2026, 8, 17));

        Assert.Equal(42, viewModel.MonthDays.Count);
        Assert.Equal(6, viewModel.MonthWeekRowCount);
        Assert.Equal(new DateOnly(2026, 7, 26), viewModel.MonthDays[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 5), viewModel.MonthDays[^1].Date);
    }

    [Fact]
    public void FirstDaySettingCanChangeTheSameMonthFromFiveToSixRows()
    {
        var sundayFirst = Create(
            new DateOnly(2022, 5, 17),
            firstDay: FirstDayOfWeekMode.Sunday);
        var mondayFirst = Create(
            new DateOnly(2022, 5, 17),
            firstDay: FirstDayOfWeekMode.Monday);

        Assert.Equal(5, sundayFirst.MonthWeekRowCount);
        Assert.Equal(6, mondayFirst.MonthWeekRowCount);
        Assert.Equal(DayOfWeek.Sunday, sundayFirst.MonthDays[0].Date.DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, mondayFirst.MonthDays[0].Date.DayOfWeek);
    }

    [Fact]
    public void LeapYearFebruary_GeneratesFebruaryTwentyNine()
    {
        var viewModel = Create(new DateOnly(2024, 2, 14));

        Assert.Contains(viewModel.MonthDays,
            day => day.Date == new DateOnly(2024, 2, 29) && day.IsCurrentMonth);
        Assert.Equal(29, viewModel.MonthDays.Count(day => day.IsCurrentMonth));
    }

    [Fact]
    public void MonthStartingOnBoundary_BeginsGridWithMonthFirst()
    {
        var viewModel = Create(
            new DateOnly(2026, 2, 11),
            firstDay: FirstDayOfWeekMode.Sunday);

        Assert.Equal(new DateOnly(2026, 2, 1), viewModel.MonthDays[0].Date);
        Assert.True(viewModel.MonthDays[0].IsCurrentMonth);
    }

    [Fact]
    public void MonthNavigation_ChangesPeriodWithoutCreatingASelection()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));

        viewModel.NavigateNextCommand.Execute(null);

        Assert.Equal(10, viewModel.DisplayedDate.Month);
        Assert.Null(viewModel.SelectedDate);
    }

    [Fact]
    public void Navigation_PreservesExistingSelection()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));
        var selected = new DateOnly(2026, 9, 9);
        viewModel.SelectDateCommand.Execute(selected);

        viewModel.NavigateNextCommand.Execute(null);

        Assert.Equal(selected, viewModel.SelectedDate);
        Assert.Equal(10, viewModel.DisplayedDate.Month);
    }

    [Fact]
    public void ExplicitDateSelection_PropagatesToMonthAndDayPanel()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));
        var selected = new DateOnly(2026, 9, 9);

        viewModel.SelectDateCommand.Execute(selected);

        Assert.Equal(selected, viewModel.SelectedDate);
        Assert.Equal(selected, viewModel.DayPanel.Date);
        Assert.True(viewModel.DayPanel.HasSelection);
        Assert.Single(viewModel.MonthDays, day => day.IsSelected);
    }

    [Fact]
    public void AdjacentMonthSelection_NavigatesAndSelects()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));
        var adjacent = viewModel.MonthDays.First(day => !day.IsCurrentMonth).Date;

        viewModel.SelectDateCommand.Execute(adjacent);

        Assert.Equal(adjacent, viewModel.SelectedDate);
        Assert.Equal(adjacent.Month, viewModel.DisplayedDate.Month);
        Assert.Contains(viewModel.MonthDays,
            day => day.Date == adjacent && day.IsCurrentMonth && day.IsSelected);
    }

    [Fact]
    public void Today_NavigatesSelectsAndClosesHeader()
    {
        var today = new DateOnly(2026, 9, 17);
        var viewModel = Create(today);
        viewModel.IsHeaderExpanded = true;
        viewModel.NavigateNextCommand.Execute(null);

        viewModel.GoToTodayCommand.Execute(null);

        Assert.Equal(today, viewModel.DisplayedDate);
        Assert.Equal(today, viewModel.SelectedDate);
        Assert.False(viewModel.IsHeaderExpanded);
    }

    [Fact]
    public void WeekGeneration_ContainsExactlySevenDates()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));
        viewModel.ShowWeekCommand.Execute(null);

        Assert.Equal(7, viewModel.WeekDays.Count);
        Assert.Equal(new DateOnly(2026, 9, 13), viewModel.WeekDays[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 19), viewModel.WeekDays[^1].Date);
    }

    [Fact]
    public void WeekNavigation_MovesSevenDaysAndDoesNotSelect()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));
        viewModel.ShowWeekCommand.Execute(null);
        var before = viewModel.WeekDays[0].Date;

        viewModel.NavigateNextCommand.Execute(null);

        Assert.Equal(before.AddDays(7), viewModel.WeekDays[0].Date);
        Assert.Null(viewModel.SelectedDate);
    }

    [Theory]
    [InlineData(FirstDayOfWeekMode.Sunday, DayOfWeek.Sunday)]
    [InlineData(FirstDayOfWeekMode.Monday, DayOfWeek.Monday)]
    public void ConfiguredFirstDay_ControlsMonthAndWeek(
        FirstDayOfWeekMode mode,
        DayOfWeek expected)
    {
        var viewModel = Create(new DateOnly(2026, 9, 17), firstDay: mode);
        viewModel.ShowWeekCommand.Execute(null);

        Assert.Equal(expected, viewModel.FirstDayOfWeek);
        Assert.Equal(expected, viewModel.WeekDays[0].Date.DayOfWeek);
        Assert.Equal(expected, viewModel.MonthDays[0].Date.DayOfWeek);
    }

    [Fact]
    public void SystemFirstDay_UsesInjectedSystemCulture()
    {
        var culture = (CultureInfo)new CultureInfo("en-US").Clone();
        culture.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
        var viewModel = Create(
            new DateOnly(2026, 9, 17),
            firstDay: FirstDayOfWeekMode.System,
            systemCulture: culture);

        Assert.Equal(DayOfWeek.Monday, viewModel.FirstDayOfWeek);
        Assert.Equal(DayOfWeek.Monday, viewModel.MonthDays[0].Date.DayOfWeek);
    }

    [Fact]
    public void SystemFirstDay_ParticipatesInFiveVersusSixRowCalculation()
    {
        var culture = (CultureInfo)new CultureInfo("en-US").Clone();
        culture.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
        var viewModel = Create(
            new DateOnly(2022, 5, 17),
            firstDay: FirstDayOfWeekMode.System,
            systemCulture: culture);

        Assert.Equal(DayOfWeek.Monday, viewModel.FirstDayOfWeek);
        Assert.Equal(6, viewModel.MonthWeekRowCount);
    }

    [Fact]
    public void Holidays_MapIntoMonthWeekAndDayPanel()
    {
        var holidayDate = new DateOnly(2026, 9, 17);
        var viewModel = Create(holidayDate, holidayDate: holidayDate);

        Assert.Contains(viewModel.MonthDays,
            day => day.Date == holidayDate && day.IsHoliday);

        viewModel.ShowWeekCommand.Execute(null);
        var weekDay = Assert.Single(viewModel.WeekDays, day => day.Date == holidayDate);
        Assert.Equal(["Test Holiday"], weekDay.HolidayNames);
        Assert.False(weekDay.ShowNoEvents);

        viewModel.SelectDateCommand.Execute(holidayDate);
        Assert.Equal(["Test Holiday"], viewModel.DayPanel.HolidayNames);
        Assert.False(viewModel.DayPanel.ShowNoEvents);
    }

    [Fact]
    public void DayPanel_UsesEmptyStateUntilExplicitSelection()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));

        Assert.Null(viewModel.SelectedDate);
        Assert.False(viewModel.DayPanel.HasSelection);
        Assert.Equal("Select a day", viewModel.DayPanel.EmptyStateText);

        viewModel.SelectDateCommand.Execute(new DateOnly(2026, 9, 18));
        Assert.True(viewModel.DayPanel.ShowNoEvents);
    }

    [Fact]
    public void ResolvedEnglishAndHebrewNamesUseGregorianDates()
    {
        var date = new DateOnly(2026, 9, 17);
        var english = Create(date, language: CalendarLanguageMode.English, holidayDate: date);
        var hebrew = Create(date, language: CalendarLanguageMode.Hebrew, holidayDate: date);

        Assert.Contains("September", english.PeriodTitle);
        Assert.Contains("ספטמבר", hebrew.PeriodTitle);

        english.ShowWeekCommand.Execute(null);
        hebrew.ShowWeekCommand.Execute(null);

        Assert.StartsWith("Thursday", english.WeekDays.Single(day => day.Date == date).DateTitle);
        Assert.Contains("יום חמישי", hebrew.WeekDays.Single(day => day.Date == date).DateTitle);
        Assert.Equal(["Test Holiday"], english.WeekDays.Single(day => day.Date == date).HolidayNames);
        Assert.Equal(["חג בדיקה"], hebrew.WeekDays.Single(day => day.Date == date).HolidayNames);
        Assert.Equal(2026, hebrew.WeekDays.Single(day => day.Date == date).Date.Year);
    }

    [Fact]
    public void HumanReadableDatesUseLocalizedEnglishAndHebrewOrder()
    {
        var date = new DateOnly(2026, 9, 17);
        var english = Create(date, language: CalendarLanguageMode.English);
        var hebrew = Create(date, language: CalendarLanguageMode.Hebrew);

        Assert.Equal("Today · Sep 17", english.TodayIndicatorText);
        Assert.StartsWith("היום · 17 ב", hebrew.TodayIndicatorText);

        english.SelectDateCommand.Execute(date);
        hebrew.SelectDateCommand.Execute(date);
        Assert.StartsWith("Thursday, September 17", english.DayPanel.DateTitle);
        Assert.Contains("17 בספטמבר", hebrew.DayPanel.DateTitle);

        english.ShowWeekCommand.Execute(null);
        hebrew.ShowWeekCommand.Execute(null);
        var englishHeading = english.WeekDays.Single(day => day.Date == date).DateTitle;
        var hebrewHeading = hebrew.WeekDays.Single(day => day.Date == date).DateTitle;
        Assert.Contains("September 17", englishHeading);
        Assert.Contains("17 בספטמבר", hebrewHeading);
    }

    [Fact]
    public void UseAppLanguageRefreshesImmediatelyWhileExplicitCalendarOverrideWins()
    {
        var appLanguage = ApplicationLanguageMode.English;
        var resolver = new CalendarLanguageResolver(
            () => appLanguage,
            new ApplicationLanguageResolver(() => new CultureInfo("he-IL")));
        var viewModel = new CalendarToolViewModel(
            new CalendarSettings { Language = CalendarLanguageMode.UseAppLanguage },
            resolver,
            new TestHolidayProvider(null),
            () => new DateOnly(2026, 9, 17));

        Assert.Equal(CalendarDisplayLanguage.English, viewModel.DisplayLanguage);
        appLanguage = ApplicationLanguageMode.Hebrew;
        viewModel.RefreshApplicationLanguage();
        Assert.Equal(CalendarDisplayLanguage.Hebrew, viewModel.DisplayLanguage);

        viewModel.SelectedLanguage = CalendarLanguageMode.English;
        appLanguage = ApplicationLanguageMode.Hebrew;
        viewModel.RefreshApplicationLanguage();
        Assert.Equal(CalendarDisplayLanguage.English, viewModel.DisplayLanguage);
    }

    [Fact]
    public void ViewToggle_PreservesSingleDisplayedAndSelectedState()
    {
        var date = new DateOnly(2026, 9, 17);
        var viewModel = Create(date);
        viewModel.SelectDateCommand.Execute(date);

        viewModel.ShowWeekCommand.Execute(null);

        Assert.True(viewModel.IsWeekView);
        Assert.False(viewModel.IsMonthView);
        Assert.Equal(date, viewModel.SelectedDate);
        Assert.Contains(viewModel.WeekDays, day => day.Date == date && day.IsSelected);

        viewModel.ShowMonthCommand.Execute(null);
        Assert.True(viewModel.IsMonthView);
        Assert.Equal(date, viewModel.SelectedDate);
    }

    [Fact]
    public void QuickAdd_WithoutSelectionOpensDatePrompt()
    {
        var viewModel = Create(new DateOnly(2026, 9, 17));

        viewModel.BeginQuickAddCommand.Execute(null);

        Assert.True(viewModel.IsQuickAddDatePromptOpen);
    }

    private static CalendarToolViewModel Create(
        DateOnly today,
        CalendarLanguageMode language = CalendarLanguageMode.English,
        FirstDayOfWeekMode firstDay = FirstDayOfWeekMode.Sunday,
        DateOnly? holidayDate = null,
        CultureInfo? systemCulture = null) => new(
            new CalendarSettings
            {
                Language = language,
                FirstDayOfWeek = firstDay,
                DefaultView = CalendarView.Month,
                ShowHolidays = true
            },
            new CalendarLanguageResolver(() => new CultureInfo("en-US")),
            new TestHolidayProvider(holidayDate),
            () => today,
            systemCulture);

    private sealed class TestHolidayProvider(DateOnly? holidayDate) : IHolidayProvider
    {
        public IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date) =>
            date == holidayDate
                ? [new CalendarHoliday(date, "Test Holiday", "חג בדיקה")]
                : [];
    }
}
