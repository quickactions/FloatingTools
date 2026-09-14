using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class CalendarEventsStageOneRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandedRowReplacesPreviewAndRetainsTextDirectionAndLtrDate(bool week)
        => WpfTestApplication.Run(() =>
        {
            var entry = new CalendarEntry
            {
                Id = Guid.NewGuid(),
                Date = new DateOnly(2026, 9, 8),
                Text = "3 דברים חשובים למחר"
            };
            var viewModel = CreateViewModel(entry);
            if (week) viewModel.ShowWeekCommand.Execute(null);
            viewModel.OpenContextualEventsCommand.Execute(null);
            Assert.True(viewModel.IsCalendarPage);
            Assert.False(viewModel.IsEventsPage);
            var item = Assert.Single(viewModel.ContextualEventsViewModel!.Items);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = new Window
            {
                Content = view,
                Width = 300,
                Height = 458,
                Left = -10000,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.True(((FrameworkElement)view.FindName("CalendarBody")).IsVisible);
                Assert.True(((FrameworkElement)view.FindName("CalendarPeriodRegion")).IsVisible);
                Assert.True(((FrameworkElement)view.FindName("ContextualEventsPanel")).IsVisible);
                Assert.False(((FrameworkElement)view.FindName("DayPanelScrollViewer")).IsVisible);
                var row = Descendants<FrameworkElement>(view)
                    .Single(element => element.Name == "ContextualEventRow"
                        && ReferenceEquals(element.DataContext, item));
                var preview = Descendants<TextBlock>(row)
                    .Single(element => element.Name == "ContextualEventPreview");
                var full = Descendants<TextBox>(row)
                    .Single(element => element.Name == "ContextualEventFullText");
                var date = Descendants<TextBlock>(row)
                    .Single(element => element.Text == "08/09/2026");
                var expand = Descendants<Button>(row)
                    .Single(element => element.Name == "ContextualEventExpandButton");

                Assert.Equal(Visibility.Visible, preview.Visibility);
                Assert.Equal(Visibility.Collapsed, full.Visibility);
                Assert.Equal(FlowDirection.RightToLeft, preview.FlowDirection);
                Assert.Equal(TextAlignment.Left, preview.TextAlignment);
                Assert.Equal(FlowDirection.LeftToRight, date.FlowDirection);
                Assert.Equal(Visibility.Visible, expand.Visibility);

                item.ToggleExpandedCommand.Execute(null);
                window.UpdateLayout();

                Assert.Equal(Visibility.Collapsed, preview.Visibility);
                Assert.Equal(Visibility.Visible, full.Visibility);
                Assert.Equal(entry.Text, full.Text);
                Assert.Equal(FlowDirection.RightToLeft, full.FlowDirection);
                Assert.Equal(TextAlignment.Left, full.TextAlignment);
            }
            finally
            {
                window.Close();
            }
        });

    [Theory]
    [InlineData("Short event", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFG", true)]
    public void PreviewTruncationControlsExpandButtonAndCommand(string text, bool isTruncated)
        => WpfTestApplication.Run(() =>
        {
            var entry = new CalendarEntry
            {
                Id = Guid.NewGuid(), Date = new DateOnly(2026, 9, 8), Text = text
            };
            var viewModel = CreateViewModel(entry);
            viewModel.OpenContextualEventsCommand.Execute(null);
            var item = Assert.Single(viewModel.ContextualEventsViewModel!.Items);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = new Window { Content = view, Width = 300, Height = 458, Left = -10000,
                ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var row = Descendants<FrameworkElement>(view)
                    .Single(element => element.Name == "ContextualEventRow"
                        && ReferenceEquals(element.DataContext, item));
                var expand = Descendants<Button>(row)
                    .Single(element => element.Name == "ContextualEventExpandButton");

                Assert.Equal(isTruncated, item.HasHiddenContent);
                Assert.Equal(isTruncated, item.ToggleExpandedCommand.CanExecute(null));
                Assert.Equal(isTruncated ? Visibility.Visible : Visibility.Collapsed, expand.Visibility);
                item.ToggleExpandedCommand.Execute(null);
                Assert.Equal(isTruncated, item.IsExpanded);
            }
            finally { window.Close(); }
        });

    [Fact]
    public void ResizingCalendarUpdatesTheSharedLayoutModeAndOpenPreview()
        => WpfTestApplication.Run(() =>
        {
            var entry = new CalendarEntry
            {
                Id = Guid.NewGuid(), Date = new DateOnly(2026, 9, 8),
                Text = "one two three four five six seven eight"
            };
            var viewModel = CreateViewModel(entry);
            viewModel.OpenContextualEventsCommand.Execute(null);
            var events = viewModel.ContextualEventsViewModel!;
            events.ToggleSortCommand.Execute(null);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = new Window { Content = view, Width = 300, Height = 760, Left = -10000,
                ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var compactItem = Assert.Single(events.Items);
                Assert.Equal("one two three…", compactItem.Preview);
                compactItem.ToggleExpandedCommand.Execute(null);
                Assert.True(compactItem.IsExpanded);

                window.Width = 560;
                window.UpdateLayout();
                var largeItem = Assert.Single(events.Items);
                Assert.Same(events, viewModel.ContextualEventsViewModel);
                Assert.Equal("one two three four five six seven…", largeItem.Preview);
                Assert.True(largeItem.HasHiddenContent);
                Assert.True(largeItem.IsExpanded);
                Assert.True(events.IsDescending);

                window.Width = 300;
                window.UpdateLayout();
                var compactAgain = Assert.Single(events.Items);
                Assert.Equal("one two three…", compactAgain.Preview);
                Assert.True(compactAgain.IsExpanded);
                Assert.Equal(entry.Id, compactAgain.Id);
            }
            finally { window.Close(); }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedSplitterResizesContextualAndDayContentWithoutResettingSize(bool initiallyExpanded)
        => WpfTestApplication.Run(() =>
        {
            var vm = CreateViewModel(new CalendarEntry { Date = new DateOnly(2026, 9, 8), Text = "Event" });
            vm.SelectDateCommand.Execute(new DateOnly(2026, 9, 8));
            vm.IsDayPanelExpanded = initiallyExpanded;
            var view = new CalendarToolView { DataContext = vm };
            var window = new Window { Content = view, Width = 300, Height = 458, Left = -10000,
                ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                vm.OpenContextualEventsCommand.Execute(null);
                window.UpdateLayout();
                var splitter = (GridSplitter)view.FindName("DayPanelSplitter");
                var content = (Border)view.FindName("DayPanelExpandedContent");
                var header = (Grid)view.FindName("ContextualEventsHeader");
                var height = content.ActualHeight;
                var windowHeight = window.Height;
                Assert.True(splitter.IsEnabled);
                void Drag(double delta)
                {
                    splitter.RaiseEvent(new DragDeltaEventArgs(0, delta) { RoutedEvent = Thumb.DragDeltaEvent });
                    window.UpdateLayout();
                }
                Drag(-40);
                Assert.Equal(height + 40, content.ActualHeight, 3);
                Drag(20);
                Assert.Equal(height + 20, content.ActualHeight, 3);
                var retained = content.Height;
                var headerHeight = header.ActualHeight;
                vm.ContextualEventsViewModel!.ToggleSortCommand.Execute(null);
                window.UpdateLayout();
                Assert.Equal(headerHeight, header.ActualHeight);
                vm.CloseContextualEventsCommand.Execute(null);
                Assert.Equal(initiallyExpanded, vm.IsDayPanelExpanded);
                Assert.Equal(retained, content.Height);
                vm.IsDayPanelExpanded = true;
                window.UpdateLayout();
                Assert.True(splitter.IsEnabled);
                Drag(-20);
                Assert.Equal(retained + 20, content.ActualHeight, 3);
                vm.OpenContextualEventsCommand.Execute(null);
                window.UpdateLayout();
                Assert.Equal(retained + 20, content.ActualHeight, 3);
                Assert.Equal(windowHeight, window.Height);
            }
            finally { window.Close(); }
        });

    [Theory]
    [InlineData(CalendarLanguageMode.English)]
    [InlineData(CalendarLanguageMode.Hebrew)]
    public void CalendarEventScrollersKeepSharedContentGapAndOuterGutter(
        CalendarLanguageMode language)
        => WpfTestApplication.Run(() =>
        {
            var date = new DateOnly(2026, 9, 8);
            var entries = Enumerable.Range(1, 24)
                .Select(index => new CalendarEntry
                {
                    Id = Guid.NewGuid(), Date = date, Text = $"Event {index}"
                })
                .ToArray();
            var viewModel = CreateViewModel(entries, language);
            viewModel.SelectDateCommand.Execute(date);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = new Window { Content = view, Width = 300, Height = 458, Left = -10000,
                ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var expanded = (Border)view.FindName("DayPanelExpandedContent");
                var dayScroller = (ScrollViewer)view.FindName("DayPanelScrollViewer");
                AssertScrollSpacing(dayScroller, expanded, expectedOuterGutter: 8);

                viewModel.OpenContextualEventsCommand.Execute(null);
                window.UpdateLayout();
                var contextualScroller = (ScrollViewer)view.FindName("ContextualEventsScrollViewer");
                AssertScrollSpacing(contextualScroller, expanded, expectedOuterGutter: 8);

                viewModel.OpenEventsCommand.Execute(null);
                window.UpdateLayout();
                var eventsShell = (FrameworkElement)view.FindName("CalendarEventsShell");
                var fullScroller = Descendants<ScrollViewer>(eventsShell)
                    .Single(scroller => scroller.Name == "BodyScrollViewer");
                AssertScrollSpacing(fullScroller, eventsShell, expectedOuterGutter: 4);
            }
            finally { window.Close(); }
        });

    private static CalendarToolViewModel CreateViewModel(CalendarEntry entry)
        => CreateViewModel([entry], CalendarLanguageMode.English);

    private static CalendarToolViewModel CreateViewModel(
        IReadOnlyList<CalendarEntry> entries,
        CalendarLanguageMode language)
    {
        var today = entries[0].Date;
        var viewModel = new CalendarToolViewModel(
            new CalendarSettings
            {
                Language = language,
                FirstDayOfWeek = FirstDayOfWeekMode.Sunday
            },
            new CalendarLanguageResolver(() => new CultureInfo("en-US")),
            new EmptyHolidayProvider(),
            todayProvider: () => today,
            systemCulture: new CultureInfo("en-US"),
            calendarStore: new EntryStore(entries));
        viewModel.InitializeAsync().GetAwaiter().GetResult();
        return viewModel;
    }

    private static void AssertScrollSpacing(
        ScrollViewer scroller,
        FrameworkElement outer,
        double expectedOuterGutter)
    {
        scroller.ApplyTemplate();
        var presenter = (ScrollContentPresenter)scroller.Template.FindName(
            "PART_ScrollContentPresenter", scroller);
        var scrollBar = (ScrollBar)scroller.Template.FindName(
            "PART_VerticalScrollBar", scroller);
        var presenterBounds = presenter.TransformToAncestor(outer)
            .TransformBounds(new Rect(presenter.RenderSize));
        var scrollBarBounds = scrollBar.TransformToAncestor(outer)
            .TransformBounds(new Rect(scrollBar.RenderSize));
        var contentGap = scrollBarBounds.Left - presenterBounds.Right;
        var outerGutter = outer.ActualWidth - scrollBarBounds.Right;

        Assert.True(scroller.ScrollableHeight > 0);
        Assert.True(scrollBar.IsVisible);
        Assert.InRange(contentGap, 1.5, 2.5);
        Assert.InRange(outerGutter, expectedOuterGutter - .5, expectedOuterGutter + .5);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private sealed class EmptyHolidayProvider : IHolidayProvider
    {
        public IReadOnlyList<CalendarHoliday> GetHolidays(DateOnly date) => [];
    }

    private sealed class EntryStore(IReadOnlyList<CalendarEntry> entries) : ICalendarStore
    {
        public Task<CalendarState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CalendarState { Entries = entries.ToList() });

        public Task SaveAsync(CalendarState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
