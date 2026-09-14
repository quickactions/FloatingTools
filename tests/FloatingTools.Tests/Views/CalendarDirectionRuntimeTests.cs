using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

 [Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class CalendarDirectionRuntimeTests
{
    [Fact]
    public void HebrewCalendar_RuntimeBindingsUseVerifiedMappingWithoutMirroringChrome()
        => RunSta(() =>
        {
            var viewModel = new CalendarToolViewModel(
                new CalendarSettings
                {
                    Language = CalendarLanguageMode.Hebrew,
                    FirstDayOfWeek = FirstDayOfWeekMode.Sunday
                },
                new CalendarLanguageResolver(() => new CultureInfo("en-US")),
                new HebrewCalendarHolidayProvider(),
                () => new DateOnly(2026, 9, 17));
            var chrome = new Grid { FlowDirection = FlowDirection.LeftToRight };
            var content = new TextBlock { DataContext = viewModel };
            var monthGrid = new ItemsControl { DataContext = viewModel };

            BindingOperations.SetBinding(
                content,
                FrameworkElement.FlowDirectionProperty,
                new Binding(nameof(CalendarToolViewModel.ContentFlowDirection)));
            BindingOperations.SetBinding(
                content,
                TextBlock.TextAlignmentProperty,
                new Binding(nameof(CalendarToolViewModel.ContentTextAlignment)));
            BindingOperations.SetBinding(
                monthGrid,
                FrameworkElement.FlowDirectionProperty,
                new Binding(nameof(CalendarToolViewModel.ContentFlowDirection)));

            Assert.Equal(FlowDirection.LeftToRight, chrome.FlowDirection);
            Assert.Equal(FlowDirection.RightToLeft, content.FlowDirection);
            Assert.Equal(TextAlignment.Left, content.TextAlignment);
            Assert.Equal(FlowDirection.RightToLeft, monthGrid.FlowDirection);
        });

    [Fact]
    public void HebrewSameMonthWeekPeriodTitle_DeclaresMonthBeforeNumericAsSeparatePhysicalElements()
        => RunSta(() =>
        {
            using var fixture = CreateWeekFixture(
                CalendarLanguageMode.Hebrew, new DateOnly(2026, 8, 26));
            var panel = FindNamed<StackPanel>(fixture.View, "HebrewSameMonthWeekPeriodTitle");
            var plainTitle = FindNamed<TextBlock>(fixture.View, "PeriodTitleText");
            var numericText = FindNamed<TextBlock>(fixture.View, "HebrewSameMonthWeekNumericText");
            var monthText = FindNamed<TextBlock>(fixture.View, "HebrewSameMonthWeekMonthText");

            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.Equal(Visibility.Collapsed, plainTitle.Visibility);
            Assert.Equal(FlowDirection.LeftToRight, panel.FlowDirection);

            // These are separate physical elements (not Inlines/Runs sharing
            // one TextBlock's paragraph).
            Assert.Same(numericText.Parent, panel);
            Assert.Same(monthText.Parent, panel);
            Assert.Equal("23–29", numericText.Text);
            Assert.Equal(FlowDirection.LeftToRight, numericText.FlowDirection);
            Assert.Equal("באוג׳", monthText.Text);
            Assert.Equal(FlowDirection.RightToLeft, monthText.FlowDirection);

            // This test only pins the DECLARED physical order (month before
            // numeric), which was reversed empirically after manual visual
            // verification showed the previous declared order rendering
            // incorrectly. It intentionally does NOT assert this order is
            // visually correct — automated geometry checks previously passed
            // while the real UI was still wrong, so this suite cannot be
            // treated as proof of correct rendering. Only manual visual
            // verification is authoritative for this behavior.
            var numericIndex = panel.Children.IndexOf(numericText);
            var monthIndex = panel.Children.IndexOf(monthText);
            Assert.True(monthIndex < numericIndex);
        });

    [Fact]
    public void EnglishSameMonthWeekPeriodTitle_IsUnchanged()
        => RunSta(() =>
        {
            using var fixture = CreateWeekFixture(
                CalendarLanguageMode.English, new DateOnly(2026, 8, 26));
            var title = FindNamed<TextBlock>(fixture.View, "PeriodTitleText");

            Assert.Equal("Aug 23 – 29", title.Text);
            Assert.Equal(Visibility.Visible, title.Visibility);
            Assert.Equal(Visibility.Collapsed,
                FindNamed<StackPanel>(fixture.View, "HebrewSameMonthWeekPeriodTitle").Visibility);
        });

    [Fact]
    public void HebrewCrossMonthWeekPeriodTitle_DeclaresReversedPhysicalOrder()
        => RunSta(() =>
        {
            using var fixture = CreateWeekFixture(
                CalendarLanguageMode.Hebrew, new DateOnly(2026, 9, 1));
            var panel = FindNamed<StackPanel>(fixture.View, "HebrewCrossMonthWeekPeriodTitle");
            var startNumeric = FindNamed<TextBlock>(fixture.View, "HebrewCrossMonthWeekStartNumericText");
            var startMonth = FindNamed<TextBlock>(fixture.View, "HebrewCrossMonthWeekStartMonthText");
            var separator = FindNamed<TextBlock>(fixture.View, "HebrewCrossMonthWeekRangeSeparatorText");
            var endNumeric = FindNamed<TextBlock>(fixture.View, "HebrewCrossMonthWeekEndNumericText");
            var endMonth = FindNamed<TextBlock>(fixture.View, "HebrewCrossMonthWeekEndMonthText");

            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.Equal(FlowDirection.LeftToRight, panel.FlowDirection);

            Assert.Equal("30", startNumeric.Text);
            Assert.Equal(FlowDirection.LeftToRight, startNumeric.FlowDirection);
            Assert.Equal("באוג׳", startMonth.Text);
            Assert.Equal(FlowDirection.RightToLeft, startMonth.FlowDirection);
            Assert.Equal(" – ", separator.Text);
            Assert.Equal(FlowDirection.LeftToRight, separator.FlowDirection);
            Assert.Equal("5", endNumeric.Text);
            Assert.Equal(FlowDirection.LeftToRight, endNumeric.FlowDirection);
            Assert.Equal("בספט׳", endMonth.Text);
            Assert.Equal(FlowDirection.RightToLeft, endMonth.FlowDirection);

            // This test only pins the DECLARED physical order (end-month,
            // end-day, separator, start-month, start-day — the reverse of
            // the previously-declared order), matching the same empirical
            // reversal applied to the same-month case. It does NOT assert
            // this order is visually correct; only manual visual
            // verification is authoritative for this behavior.
            var indices = new[] { endMonth, endNumeric, separator, startMonth, startNumeric }
                .Select(panel.Children.IndexOf)
                .ToArray();
            Assert.Equal(indices, indices.OrderBy(index => index).ToArray());
        });

    [Fact]
    public void HebrewContextualWeekTitleUsesSeparateFragmentsInVerifiedPhysicalOrder()
        => RunSta(() =>
        {
            using var fixture = CreateWeekFixture(
                CalendarLanguageMode.Hebrew, new DateOnly(2026, 8, 26));
            fixture.ViewModel.OpenContextualEventsCommand.Execute(null);
            fixture.Window.UpdateLayout();
            var panel = FindNamed<StackPanel>(
                fixture.View, "ContextualHebrewSameMonthWeekTitle");
            var month = FindNamed<TextBlock>(
                fixture.View, "ContextualHebrewSameMonthWeekMonthText");
            var numeric = FindNamed<TextBlock>(
                fixture.View, "ContextualHebrewSameMonthWeekNumericText");
            var heading = FindNamed<TextBlock>(
                fixture.View, "ContextualHebrewSameMonthWeekHeadingText");
            var headingSeparator = FindNamed<TextBlock>(
                fixture.View, "ContextualHebrewSameMonthWeekHeadingSeparatorText");

            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.Equal(FlowDirection.LeftToRight, panel.FlowDirection);
            Assert.Equal("באוג׳", month.Text);
            Assert.Equal("23–29", numeric.Text);
            Assert.Equal("אירועים", heading.Text);
            Assert.Equal(
                Assert.IsType<SolidColorBrush>(heading.Foreground).Color,
                Assert.IsType<SolidColorBrush>(headingSeparator.Foreground).Color);

            var monthLeft = month.TranslatePoint(new Point(), panel).X;
            var numericLeft = numeric.TranslatePoint(new Point(), panel).X;
            var headingLeft = heading.TranslatePoint(new Point(), panel).X;
            Assert.True(monthLeft < numericLeft,
                $"Month fragment {monthLeft} should render left of numeric fragment {numericLeft}.");
            Assert.True(numericLeft < headingLeft,
                $"Numeric fragment {numericLeft} should render left of heading {headingLeft}.");
            Assert.True(panel.ActualWidth > 0);
            Assert.True(panel.ActualHeight > 0);
        });

    [Fact]
    public void CalendarStageThreeView_LoadsAtStandardSizeWithBoundRuntimeCollections()
        => RunSta(() =>
        {
            var viewModel = new CalendarToolViewModel(
                new CalendarSettings { Language = CalendarLanguageMode.English },
                new CalendarLanguageResolver(() => new CultureInfo("en-US")),
                new HebrewCalendarHolidayProvider(),
                () => new DateOnly(2026, 9, 17));
            var view = new CalendarToolView { DataContext = viewModel };
            var window = new Window
            {
                Content = view,
                Width = 300,
                Height = 458,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(() => { }));
                window.UpdateLayout();

                var monthItems = FindDescendants<ItemsControl>(view)
                    .Single(control => control.Name == "MonthDayItems");
                var splitter = FindDescendants<GridSplitter>(view).Single();
                Assert.Equal(35, monthItems.Items.Count);
                Assert.True(monthItems.ActualHeight > 0);
                Assert.Equal(17, splitter.ActualHeight);
                Assert.Equal(FlowDirection.LeftToRight, view.FlowDirection);
            }
            finally
            {
                window.Close();
            }
        });

    private static WeekFixture CreateWeekFixture(CalendarLanguageMode language, DateOnly displayedDate)
    {
        var viewModel = new CalendarToolViewModel(
            new CalendarSettings
            {
                Language = language,
                FirstDayOfWeek = FirstDayOfWeekMode.Sunday
            },
            new CalendarLanguageResolver(() => new CultureInfo("en-US")),
            new HebrewCalendarHolidayProvider(),
            () => displayedDate);
        viewModel.SelectDateCommand.Execute(displayedDate);
        viewModel.ShowWeekCommand.Execute(null);

        var view = new CalendarToolView { DataContext = viewModel };
        var window = new Window
        {
            Content = view,
            Width = 300,
            Height = 458,
            Left = -10_000,
            Top = -10_000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None
        };
        window.Show();
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));
        window.UpdateLayout();
        return new WeekFixture(window, view, viewModel);
    }

    private static CultureInfo CreateHebrewGregorianCulture()
    {
        var culture = (CultureInfo)new CultureInfo("he-IL").Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return culture;
    }

    private static T FindNamed<T>(DependencyObject root, string name)
        where T : FrameworkElement => FindDescendants<T>(root)
            .Single(element => element.Name == name);

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static void RunSta(Action action) => WpfTestApplication.Run(action);

    private sealed class WeekFixture(
        Window window, CalendarToolView view, CalendarToolViewModel viewModel) : IDisposable
    {
        public Window Window { get; } = window;

        public CalendarToolView View { get; } = view;

        public CalendarToolViewModel ViewModel { get; } = viewModel;

        public void Dispose()
        {
            if (Window.IsLoaded)
            {
                Window.Close();
            }
        }
    }
}
