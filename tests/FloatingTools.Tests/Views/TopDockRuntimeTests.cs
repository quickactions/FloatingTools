using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Views;
using FloatingTools.Tests.ViewModels;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class TopDockRuntimeTests
{
    [Theory]
    [InlineData(PanelSizePreset.Standard, 460, 258, 212, 412, 92)]
    [InlineData(PanelSizePreset.Large, 720, 518, 472, 672, 180)]
    public void TopCalendar_ScrollsMonthWhileSelectedDayStaysFixed(
        PanelSizePreset preset, double width, double height, double bodyHeight,
        double minimumMonthHeight, double dayMinimum)
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
            viewModel.SelectDateCommand.Execute(new DateOnly(2026, 9, 8));
            var view = new CalendarToolView { DataContext = viewModel };
            view.SetTopDocked(true, preset);
            var window = new Window
            {
                Content = view, Width = width, Height = height,
                Left = -10000, Top = -10000, ShowInTaskbar = false,
                ShowActivated = false, WindowStyle = WindowStyle.None
            };
            try
            {
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                view.UpdateLayout();

                var scroller = (ScrollViewer)view.FindName("TopDockCalendarScrollViewer");
                var monthScroller = (ScrollViewer)view.FindName("MonthScrollViewer");
                var weekScroller = (ScrollViewer)view.FindName("WeekScrollViewer");
                var dayScroller = (ScrollViewer)view.FindName("DayPanelScrollViewer");
                var body = (Grid)view.FindName("CalendarBody");
                var monthContent = (Grid)view.FindName("CalendarMainContent");
                var dayPanel = (Grid)view.FindName("DayPanelOverlay");
                var dayContent = (Border)view.FindName("DayPanelExpandedContent");
                Assert.Equal(ScrollBarVisibility.Auto, scroller.VerticalScrollBarVisibility);
                Assert.Same(view.FindResource("FloatingToolsSharedScrollViewerStyle"), scroller.Style);
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("MonthOverlayInset")).Visibility);
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("WeekOverlayInset")).Visibility);
                Assert.Equal(1, Grid.GetRowSpan(scroller));
                Assert.Equal(1, Grid.GetRow(dayPanel));
                Assert.Equal(1, Grid.GetRowSpan(dayPanel));
                Assert.Same(body, dayPanel.Parent);
                Assert.Equal(bodyHeight, body.MinHeight);
                Assert.Equal(minimumMonthHeight, monthContent.MinHeight);
                Assert.Equal(dayMinimum, dayContent.MinHeight);
                Assert.True(scroller.ScrollableHeight > 100);
                Assert.Equal(ScrollBarVisibility.Disabled, monthScroller.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, weekScroller.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Auto, dayScroller.VerticalScrollBarVisibility);

                var dayTop = dayPanel.TransformToAncestor(body).Transform(new Point()).Y;
                scroller.ScrollToBottom();
                view.UpdateLayout();
                Assert.InRange(dayPanel.TransformToAncestor(body).Transform(new Point()).Y,
                    dayTop - 1, dayTop + 1);
                Assert.InRange(dayPanel.TransformToAncestor(body)
                    .Transform(new Point(0, dayPanel.ActualHeight)).Y,
                    0, body.ActualHeight + 1);
                Assert.Equal(new DateOnly(2026, 9, 8), viewModel.SelectedDate);

                view.SetTopDocked(false, preset);
                view.UpdateLayout();
                Assert.Equal(ScrollBarVisibility.Disabled, scroller.VerticalScrollBarVisibility);
                Assert.Equal(2, Grid.GetRowSpan(scroller));
                Assert.Equal(0, Grid.GetRow(dayPanel));
                Assert.Equal(2, Grid.GetRowSpan(dayPanel));
                Assert.Equal(ScrollBarVisibility.Auto, monthScroller.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Auto, weekScroller.VerticalScrollBarVisibility);
                Assert.Equal(Visibility.Visible, ((Border)view.FindName("MonthOverlayInset")).Visibility);
                Assert.Equal(Visibility.Visible, ((Border)view.FindName("WeekOverlayInset")).Visibility);
                Assert.Equal(0, body.MinHeight);
                Assert.True(double.IsNaN(body.Height));
            }
            finally { window.Close(); }
        });
}
