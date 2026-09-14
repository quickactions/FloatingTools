using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Stage 3: the month grid carries no wide-only width or height sizing. It
/// stretches to its container and divides the region height across its rows, so
/// at any panel width it reaches the same boundaries relative to the content
/// area that it reaches at 300 dip. These tests assert that responsiveness
/// across several widths rather than pinning any single one.
/// </summary>
[Collection(WpfResourceCollection.Name)]
public sealed class CalendarMonthWideLayoutTests
{
    private static readonly DateOnly SixRowMonth = new(2026, 8, 17);
    private static readonly DateOnly FiveRowMonth = new(2026, 9, 17);

    [Fact]
    public void ViewModelExposesNoWideOnlyMonthSizing()
    {
        var type = typeof(CalendarToolViewModel);

        // Any of these coming back means a wide-only cap has been reintroduced
        // and the layout is no longer driven purely by the container.
        foreach (var removed in new[]
        {
            "MonthGridWidth", "MonthGridMargin", "MonthGridMaxHeight",
            "MonthGridHorizontalAlignment", "MonthLayoutWidth",
        })
        {
            Assert.Null(type.GetProperty(removed));
        }

        foreach (var removed in new[]
        {
            "MonthCellMaximumWidth", "MonthCellMaximumSize",
            "MonthRowMaximumHeight", "MonthWideSideMargin", "MonthWideSideMarginRatio",
        })
        {
            Assert.Null(type.GetField(removed));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NarrowMonthGeometryIsUnchanged(bool sixRowMonth)
        => RunMonth(300, 500, sixRowMonth, m =>
        {
            // Baseline lock on the approved narrow geometry.
            Assert.Equal(259.2, m.Grid.ActualWidth, 1);
            Assert.Equal(378.2, m.Grid.ActualHeight, 1);
            Assert.Equal(37.0, m.CellWidth, 1);

            // Fills its content area: only the scroll gutter remains.
            Assert.Equal(m.Scroller.ViewportWidth, m.Grid.ActualWidth, 1);
            Assert.Equal(0d, m.LeftGap, 1);
            Assert.Equal(0d, m.RightGap, 1);
            Assert.Equal(m.Region.ActualHeight, m.Grid.ActualHeight, 1);
            Assert.Equal(default(Thickness), m.Grid.Margin);
        });

    [Theory]
    [InlineData(560, 760)]
    [InlineData(760, 760)]
    [InlineData(1000, 800)]
    public void WideMonthFillsItsContentAreaLikeNarrowDoes(double width, double height)
        => RunMonth(width, height, sixRowMonth: false, m =>
        {
            // Exactly the narrow relationship: grid == viewport, no artificial
            // side gap, no wide-only margin.
            Assert.Equal(m.Scroller.ViewportWidth, m.Grid.ActualWidth, 1);
            Assert.Equal(0d, m.LeftGap, 1);
            Assert.Equal(0d, m.RightGap, 1);
            Assert.Equal(default(Thickness), m.Grid.Margin);

            // Rows keep dividing the region height rather than being capped.
            Assert.Equal(m.Region.ActualHeight, m.Grid.ActualHeight, 1);

            // No horizontal clipping or scrolling, ever.
            Assert.True(m.Grid.ActualWidth <= m.Scroller.ViewportWidth + 0.5);
            Assert.Equal(0d, m.Scroller.ScrollableWidth, 1);
        });

    [Fact]
    public void MonthGrowsWithTheContainerAndKeepsOnlyTheNormalGutter()
    {
        var samples = new[] { 560d, 760d, 1000d }
            .Select(width =>
            {
                Measured captured = default!;
                RunMonth(width, 800, sixRowMonth: false, m => captured = m.Snapshot());
                return (Width: width, M: captured);
            })
            .ToArray();

        // Strictly increasing: the grid tracks the container, with no cap.
        for (var i = 1; i < samples.Length; i++)
        {
            Assert.True(
                samples[i].M.GridWidth > samples[i - 1].M.GridWidth + 50,
                $"Month grid should grow with the panel: {samples[i - 1].Width:0} -> "
                + $"{samples[i].Width:0} gave {samples[i - 1].M.GridWidth:0.0} -> "
                + $"{samples[i].M.GridWidth:0.0}.");
            Assert.True(
                samples[i].M.CellWidth > samples[i - 1].M.CellWidth,
                "Day cells should grow with the panel.");
        }

        // The only space left over is the container's own gutter, and it does
        // not grow with the panel the way a proportional margin would.
        var gutters = samples
            .Select(s => s.M.RegionWidth - s.M.GridWidth)
            .ToArray();
        Assert.All(gutters, gutter => Assert.True(
            gutter < 20,
            $"Only the normal content gutter should remain, got {gutter:0.0} dip."));
        Assert.Equal(gutters[0], gutters[^1], 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WideMonthIsSubstantiallyLargerThanNarrow(bool sixRowMonth)
    {
        Measured narrow = default!, wide = default!;
        RunMonth(300, 500, sixRowMonth, m => narrow = m.Snapshot());
        RunMonth(760, 760, sixRowMonth, m => wide = m.Snapshot());

        Assert.True(
            wide.CellWidth > narrow.CellWidth * 1.3,
            $"Wide cell {wide.CellWidth:0.0} should be substantially larger than "
            + $"narrow {narrow.CellWidth:0.0}.");
        Assert.True(wide.GridWidth > narrow.GridWidth * 1.3);
    }

    private sealed record Measured(
        double RegionWidth,
        double ViewportWidth,
        double GridWidth,
        double GridHeight,
        double CellWidth,
        double LeftGap,
        double RightGap);

    private sealed class MonthProbe
    {
        public required CalendarToolViewModel ViewModel { get; init; }
        public required Grid Grid { get; init; }
        public required Grid Region { get; init; }
        public required ScrollViewer Scroller { get; init; }

        public double CellWidth => Grid.ActualWidth / 7;

        public double LeftGap =>
            Grid.TransformToAncestor(Scroller).Transform(new Point(0, 0)).X;

        public double RightGap => Scroller.ViewportWidth - Grid.ActualWidth - LeftGap;

        public Measured Snapshot() => new(
            Region.ActualWidth,
            Scroller.ViewportWidth,
            Grid.ActualWidth,
            Grid.ActualHeight,
            CellWidth,
            LeftGap,
            RightGap);
    }

    private static void RunMonth(
        double width,
        double height,
        bool sixRowMonth,
        Action<MonthProbe> assert)
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CreateCalendar(sixRowMonth ? SixRowMonth : FiveRowMonth);
            viewModel.ShowMonthCommand.Execute(null);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = new Window
            {
                Content = view,
                Width = width,
                Height = height,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                Drain(view);

                Assert.True(viewModel.IsMonthView);
                var grid = (Grid)view.FindName("MonthContentLayer");
                Assert.True(grid.IsVisible, "The month grid was not visible.");

                assert(new MonthProbe
                {
                    ViewModel = viewModel,
                    Grid = grid,
                    Region = (Grid)view.FindName("CalendarPeriodRegion"),
                    Scroller = (ScrollViewer)view.FindName("MonthScrollViewer"),
                });
            }
            finally
            {
                window.Close();
                Drain(view);
            }
        });

    private static CalendarToolViewModel CreateCalendar(DateOnly today) => new(
        new CalendarSettings { Language = CalendarLanguageMode.English },
        new CalendarLanguageResolver(),
        new HebrewCalendarHolidayProvider(),
        todayProvider: () => today,
        systemCulture: new CultureInfo("en-US"));

    private static void Drain(FrameworkElement view)
    {
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));
        view.UpdateLayout();
    }
}
