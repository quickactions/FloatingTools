using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Views;
using FloatingTools.Tests.ViewModels;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class CalendarDayPanelSizingRuntimeTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);

    [Fact]
    public void NarrowBaselineAndLargeResponsiveBoundsFollowCalendarRegionHeight()
    {
        var narrow = Measure(width: 300, height: 500);
        var large = Measure(width: 760, height: 760);

        Assert.Equal(378.2, narrow.RegionHeight, 1);
        Assert.Equal(106, narrow.PanelHeight, 1);
        Assert.Equal(CalendarDayPanelSizing.CompactMinimumHeight, narrow.MinimumHeight);
        Assert.Equal(268, narrow.MaximumHeight, 1);
        Assert.Equal(CalendarDayPanelSizing.GetDefaultHeight(
                large.RegionHeight,
                CalendarLayoutMode.Large),
            large.PanelHeight, 1);
        Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, large.MinimumHeight);
        Assert.Equal(CalendarDayPanelSizing.GetMaximumHeight(large.RegionHeight),
            large.MaximumHeight, 1);
        Assert.True(large.PanelHeight > narrow.PanelHeight * 1.5);
        Assert.True(large.MaximumHeight > narrow.MaximumHeight * 1.5);
        Assert.True(large.OverlayHeight <= large.RegionHeight);
    }

    [Fact]
    public void LargeUserHeightSurvivesGrowthAndCannotShrinkBelowLargeMinimum()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
            viewModel.SelectDateCommand.Execute(Today);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = CreateWindow(view, width: 760, height: 760);
            try
            {
                window.Show();
                Drain(view);
                var panel = (Border)view.FindName("DayPanelExpandedContent");
                var splitter = (GridSplitter)view.FindName("DayPanelSplitter");
                var customHeight = panel.ActualHeight + 30;
                Drag(splitter, -30);
                Drain(view);
                Assert.Equal(customHeight, panel.ActualHeight, 1);

                window.Height = 800;
                Drain(view);
                Assert.Equal(customHeight, panel.ActualHeight, 1);

                Drag(splitter, -1000);
                Drain(view);
                Assert.Equal(panel.MaxHeight, panel.ActualHeight, 1);
                window.Height = 500;
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.GetMaximumHeight(
                    ((Grid)view.FindName("CalendarPeriodRegion")).ActualHeight),
                    panel.ActualHeight, 1);

                Drag(splitter, 1000);
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.ActualHeight, 1);
                viewModel.BeginAddEventCommand.Execute(null);
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.MinHeight);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.ActualHeight, 1);
                viewModel.CancelEventEditorCommand.Execute(null);
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.MinHeight);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.ActualHeight, 1);
            }
            finally { window.Close(); }
        });

    [Fact]
    public void LayoutTransitionsPreserveValidUserHeightAndClampOnlyWhenMinimumIncreases()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
            viewModel.SelectDateCommand.Execute(Today);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = CreateWindow(view, width: 300, height: 500);
            try
            {
                window.Show();
                Drain(view);
                var panel = (Border)view.FindName("DayPanelExpandedContent");
                var splitter = (GridSplitter)view.FindName("DayPanelSplitter");

                Drag(splitter, 1000);
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.CompactMinimumHeight, panel.MinHeight);
                Assert.Equal(CalendarDayPanelSizing.CompactMinimumHeight, panel.ActualHeight, 1);

                viewModel.BeginAddEventCommand.Execute(null);
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.EditorMinimumHeight, panel.MinHeight);
                Assert.Equal(CalendarDayPanelSizing.EditorMinimumHeight, panel.ActualHeight, 1);
                viewModel.CancelEventEditorCommand.Execute(null);
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.CompactMinimumHeight, panel.MinHeight);
                Assert.Equal(CalendarDayPanelSizing.CompactMinimumHeight, panel.ActualHeight, 1);

                window.Width = 760;
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.MinHeight);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.ActualHeight, 1);

                window.Width = 300;
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.CompactMinimumHeight, panel.MinHeight);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.ActualHeight, 1);

                Drag(splitter, 30);
                Drain(view);
                Assert.Equal(150, panel.ActualHeight, 1);
                window.Width = 760;
                Drain(view);
                Assert.Equal(CalendarDayPanelSizing.LargeMinimumHeight, panel.ActualHeight, 1);
            }
            finally { window.Close(); }
        });

    [Fact]
    public void ContextualEventsUsesTheSameResponsivePanelAndSplitterState()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
            viewModel.SelectDateCommand.Execute(Today);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = CreateWindow(view, width: 760, height: 760);
            try
            {
                window.Show();
                Drain(view);
                var panel = (Border)view.FindName("DayPanelExpandedContent");
                var splitter = (GridSplitter)view.FindName("DayPanelSplitter");
                var initialHeight = panel.ActualHeight;

                viewModel.OpenContextualEventsCommand.Execute(null);
                Drain(view);
                Assert.True(((Grid)view.FindName("ContextualEventsPanel")).IsVisible);
                Assert.Equal(initialHeight, panel.ActualHeight, 1);
                Drag(splitter, -30);
                Drain(view);
                Assert.Equal(initialHeight + 30, panel.ActualHeight, 1);

                viewModel.CloseContextualEventsCommand.Execute(null);
                Drain(view);
                Assert.Equal(initialHeight + 30, panel.ActualHeight, 1);
            }
            finally { window.Close(); }
        });

    private static Measurement Measure(double width, double height)
    {
        Measurement measurement = default!;
        WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
            viewModel.SelectDateCommand.Execute(Today);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = CreateWindow(view, width, height);
            try
            {
                window.Show();
                Drain(view);
                var region = (Grid)view.FindName("CalendarPeriodRegion");
                var panel = (Border)view.FindName("DayPanelExpandedContent");
                var overlay = (Grid)view.FindName("DayPanelOverlay");
                measurement = new(
                    region.ActualHeight,
                    panel.ActualHeight,
                    panel.MinHeight,
                    panel.MaxHeight,
                    overlay.ActualHeight);
            }
            finally { window.Close(); }
        });
        return measurement;
    }

    private static Window CreateWindow(FrameworkElement content, double width, double height) => new()
    {
        Content = content,
        Width = width,
        Height = height,
        Left = -10_000,
        Top = -10_000,
        ShowInTaskbar = false,
        ShowActivated = false,
        WindowStyle = WindowStyle.None
    };

    private static void Drag(GridSplitter splitter, double verticalChange) =>
        splitter.RaiseEvent(new DragDeltaEventArgs(0, verticalChange)
        {
            RoutedEvent = Thumb.DragDeltaEvent
        });

    private static void Drain(FrameworkElement view)
    {
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));
        view.UpdateLayout();
    }

    private sealed record Measurement(
        double RegionHeight,
        double PanelHeight,
        double MinimumHeight,
        double MaximumHeight,
        double OverlayHeight);
}
