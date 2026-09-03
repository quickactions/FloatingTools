using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Popups;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class CalendarFocusedCorrectionRuntimeTests
{
    private static readonly DateOnly Date = new(2026, 9, 17);

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew, true)]
    [InlineData(CalendarLanguageMode.English, false)]
    public void DayPanelPlacesActionOppositeLocalizedTitleWithoutMirroringShell(
        CalendarLanguageMode language,
        bool actionShouldBeLeft)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(language, width: 300, height: 458);
            fixture.ViewModel.SelectDateCommand.Execute(Date);
            fixture.Window.UpdateLayout();
            var title = FindNamed<TextBlock>(fixture.View, "DayPanelDateTitle");
            var add = FindNamed<Button>(fixture.View, "DayPanelAddButton");
            var titleCenter = title.TranslatePoint(
                new Point(title.ActualWidth / 2, title.ActualHeight / 2), fixture.View).X;
            var actionCenter = add.TranslatePoint(
                new Point(add.ActualWidth / 2, add.ActualHeight / 2), fixture.View).X;

            Assert.Equal(FlowDirection.LeftToRight, fixture.View.FlowDirection);
            Assert.Equal(actionShouldBeLeft, actionCenter < titleCenter);
        });

    [Fact]
    public void EmptyAddEditorBackspaceOrDeleteCancelsWhileNonEmptyTextDoesNot()
        => RunSta(() =>
        {
            var viewModel = CreateViewModel(CalendarLanguageMode.English);
            var view = new CalendarToolView { DataContext = viewModel };
            viewModel.SelectDateCommand.Execute(Date);
            viewModel.BeginAddEventCommand.Execute(null);

            Assert.False(view.TryCancelEmptyAddEditor(Key.Back, "text"));
            Assert.True(viewModel.IsAddingEvent);
            Assert.True(view.TryCancelEmptyAddEditor(Key.Delete, string.Empty));
            Assert.False(viewModel.IsEventEditorOpen);
        });

    [Fact]
    public void CalendarSingleLineInputsShowTypedTextWithoutVerticalScrolling()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.Hebrew, width: 300, height: 458);
            fixture.ViewModel.BeginAddEventCommand.Execute(null);
            fixture.ViewModel.IsHeaderExpanded = true;
            fixture.Window.UpdateLayout();
            var editor = FindNamed<TextBox>(fixture.View, "EventEditorTextBox");
            var search = FindNamed<TextBox>(fixture.View, "CalendarSearchTextBox");

            editor.Text = "אירוע לבדיקה";
            search.Text = "חיפוש לבדיקה";
            fixture.Window.UpdateLayout();

            AssertSingleLineInput(editor);
            AssertSingleLineInput(search);
        });

    [Fact]
    public void LanguageRefreshKeepsOtherSettingsComboBoxesSelected()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            fixture.ViewModel.SelectedDefaultView = CalendarView.Week;
            fixture.ViewModel.SelectedFirstDayOfWeek = FirstDayOfWeekMode.Monday;
            fixture.ViewModel.ShowHolidays = false;
            fixture.ViewModel.OpenSettingsCommand.Execute(null);
            fixture.ViewModel.SelectedLanguage = CalendarLanguageMode.Hebrew;
            fixture.Window.UpdateLayout();
            var view = FindNamed<ComboBox>(fixture.View, "CalendarDefaultViewComboBox");
            var firstDay = FindNamed<ComboBox>(fixture.View, "CalendarFirstDayComboBox");

            Assert.Equal(CalendarView.Week, view.SelectedValue);
            Assert.NotNull(view.SelectedItem);
            Assert.Equal(FirstDayOfWeekMode.Monday, firstDay.SelectedValue);
            Assert.NotNull(firstDay.SelectedItem);
            Assert.False(fixture.ViewModel.ShowHolidays);
        });

    [Theory]
    [InlineData(CalendarLanguageMode.English, false)]
    [InlineData(CalendarLanguageMode.Hebrew, true)]
    public void SettingsBackButtonUsesLocalizedLeadingSideWithoutMirroringToolShell(
        CalendarLanguageMode language,
        bool backShouldBeRight)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(language, width: 300, height: 458);
            fixture.ViewModel.OpenSettingsCommand.Execute(null);
            fixture.Window.UpdateLayout();
            var back = FindNamed<Button>(fixture.View, "BackButton");
            var title = FindNamed<TextBlock>(fixture.View, "TitleText");
            var backCenter = back.TranslatePoint(
                new Point(back.ActualWidth / 2, back.ActualHeight / 2), fixture.View).X;
            var titleCenter = title.TranslatePoint(
                new Point(title.ActualWidth / 2, title.ActualHeight / 2), fixture.View).X;

            Assert.Equal(FlowDirection.LeftToRight, fixture.View.FlowDirection);
            Assert.Equal(backShouldBeRight, backCenter > titleCenter);
            Assert.Equal(
                language == CalendarLanguageMode.Hebrew
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight,
                back.FlowDirection);
            Assert.Equal(fixture.ViewModel.ContentFlowDirection, title.FlowDirection);
            Assert.Equal(fixture.ViewModel.ContentTextAlignment, title.TextAlignment);
        });

    [Fact]
    public void MonthDayPanelResizeDoesNotChangeMonthGridArrangedHeight()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            var period = FindNamed<Grid>(fixture.View, "CalendarPeriodRegion");
            var monthItems = FindNamed<ItemsControl>(fixture.View, "MonthDayItems");
            var overlay = FindNamed<Grid>(fixture.View, "DayPanelOverlay");
            var splitter = FindNamed<GridSplitter>(fixture.View, "DayPanelSplitter");
            var periodHeight = period.ActualHeight;
            var monthHeight = monthItems.ActualHeight;
            var overlayHeight = overlay.ActualHeight;

            splitter.RaiseEvent(new DragDeltaEventArgs(0, -40)
            {
                RoutedEvent = Thumb.DragDeltaEvent
            });
            fixture.Window.UpdateLayout();

            Assert.Equal(periodHeight, period.ActualHeight, precision: 3);
            Assert.Equal(monthHeight, monthItems.ActualHeight, precision: 3);
            Assert.Equal(overlayHeight + 40, overlay.ActualHeight, precision: 3);
        });

    [Fact]
    public void CollapsedDayPanelKeepsOnlyHandleAndSelectionAcrossViewSwitches()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            var overlay = FindNamed<Grid>(fixture.View, "DayPanelOverlay");
            var handle = FindNamed<GridSplitter>(fixture.View, "DayPanelSplitter");
            var content = FindNamed<Border>(fixture.View, "DayPanelExpandedContent");

            fixture.ViewModel.ToggleDayPanelCommand.Execute(null);
            fixture.Window.UpdateLayout();

            Assert.Equal(Date, fixture.ViewModel.SelectedDate);
            Assert.False(fixture.ViewModel.IsDayPanelExpanded);
            Assert.Equal(Visibility.Collapsed, content.Visibility);
            Assert.Equal(handle.ActualHeight, overlay.ActualHeight, precision: 3);
            fixture.ViewModel.ShowWeekCommand.Execute(null);
            fixture.Window.UpdateLayout();
            Assert.Equal(handle.ActualHeight, overlay.ActualHeight, precision: 3);

            fixture.ViewModel.SelectDateCommand.Execute(Date.AddDays(1));
            fixture.Window.UpdateLayout();
            Assert.True(fixture.ViewModel.IsDayPanelExpanded);
            Assert.Equal(Visibility.Visible, content.Visibility);
            Assert.True(overlay.ActualHeight >= 109);
        });

    [Fact]
    public void EventActionColumnRemainsInsideTheContinuousHoverHitSurface()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458, entryCount: 1);
            var row = FindNamed<Grid>(fixture.View, "EventRow");
            var surface = FindNamed<Border>(fixture.View, "EventSurface");
            var action = FindDescendants<Button>(row)
                .Single(button => Equals(button.ToolTip, "Event actions"));
            var hit = VisualTreeHelper.HitTest(
                row,
                new Point(row.ActualWidth - 1, row.ActualHeight / 2));

            Assert.NotNull(row.Background);
            Assert.Equal(2, Grid.GetColumnSpan(surface));
            Assert.False(surface.IsHitTestVisible);
            Assert.NotNull(hit);
            Assert.Contains(hit!.VisualHit, FindDescendants<DependencyObject>(row));
            Assert.Contains(action, FindDescendants<Button>(row));
        });

    [Theory]
    [InlineData(false, 0, 92)]
    [InlineData(false, 4, 92)]
    [InlineData(true, 4, 92)]
    [InlineData(false, 4, 200)]
    [InlineData(true, 4, 200)]
    public void AddModeScrollsTheCompleteEditorBlockIntoTheSharedDayPanelViewport(
        bool weekView,
        int entryCount,
        double contentHeight)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458, entryCount);
            if (weekView)
            {
                fixture.ViewModel.ShowWeekCommand.Execute(null);
            }

            var expandedContent = FindNamed<Border>(fixture.View, "DayPanelExpandedContent");
            var scroller = FindNamed<ScrollViewer>(fixture.View, "DayPanelScrollViewer");
            expandedContent.Height = contentHeight;
            scroller.ScrollToTop();
            fixture.Window.UpdateLayout();

            fixture.ViewModel.BeginAddEventCommand.Execute(null);
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() => { }));
            fixture.Window.UpdateLayout();
            var editorBlock = FindNamed<Border>(fixture.View, "EventEditorSection");
            var save = FindNamed<Button>(fixture.View, "EventSaveButton");
            var cancel = FindNamed<Button>(fixture.View, "EventCancelButton");
            var blockTop = editorBlock.TranslatePoint(new Point(), scroller).Y;
            var blockBottom = editorBlock.TranslatePoint(
                new Point(0, editorBlock.ActualHeight), scroller).Y;

            Assert.True(fixture.ViewModel.IsAddingEvent);
            Assert.True(blockTop >= -0.5,
                $"Editor top {blockTop} should be inside the viewport.");
            Assert.True(blockBottom <= scroller.ViewportHeight + 0.5,
                $"Editor bottom {blockBottom} should be inside viewport {scroller.ViewportHeight}.");
            Assert.True(save.IsVisible);
            Assert.True(cancel.IsVisible);
        });

    [Theory]
    [InlineData(300, 458, false)]
    [InlineData(430, 640, false)]
    [InlineData(300, 458, true)]
    [InlineData(430, 640, true)]
    public void ProductionPopupWiringOpensAndClampsSelectorAndEntryActions(
        double width,
        double height,
        bool entryActions)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width, height, entryCount: entryActions ? 1 : 0);
            FrameworkElement target;
            if (entryActions)
            {
                target = FindDescendants<Button>(fixture.View)
                    .Single(button => Equals(button.ToolTip, "Event actions"));
            }
            else
            {
                target = FindNamed<Button>(fixture.View, "ViewSelectorButton");
            }

            target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() => { }));

            var host = FindNamed<AnchoredPopupHost>(fixture.View, "CalendarPopupHost");
            Assert.True(host.IsOpen);
            Assert.True(host.IsPopupOpen);
            Assert.Same(fixture.View, host.Request!.ClampBoundsElement);
            Assert.InRange(host.CurrentPlacement.Position.X, 0, fixture.View.ActualWidth);
            Assert.InRange(host.CurrentPlacement.Position.Y, 0, fixture.View.ActualHeight);
            Assert.True(host.CurrentPlacement.Position.X + host.MeasuredPopupSize.Width
                <= fixture.View.ActualWidth + 0.5);
            Assert.True(host.CurrentPlacement.Position.Y + host.MeasuredPopupSize.Height
                <= fixture.View.ActualHeight + 0.5);
            host.Close();
        });

    [Fact]
    public void WeekShowsOnlyInteriorSeparatorsBetweenWrappedEvents()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.Hebrew, width: 300, height: 458, entryCount: 4);
            fixture.ViewModel.ShowWeekCommand.Execute(null);
            fixture.Window.UpdateLayout();
            var separators = FindDescendants<TextBlock>(fixture.View)
                .Where(text => text.Text == "•")
                .ToArray();

            Assert.Equal(4, separators.Length);
            Assert.Single(separators, separator => separator.Visibility == Visibility.Collapsed);
            Assert.Equal(3, separators.Count(separator => separator.Visibility == Visibility.Visible));
        });

    [Theory]
    [InlineData(109)]
    [InlineData(285)]
    public void WeekCanScrollSaturdayFullyAboveDayPanelAtOverlayBounds(double overlayHeight)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            fixture.ViewModel.ShowWeekCommand.Execute(null);
            var overlay = FindNamed<Grid>(fixture.View, "DayPanelOverlay");
            var expandedContent = FindNamed<Border>(fixture.View, "DayPanelExpandedContent");
            expandedContent.Height = overlayHeight - 17;
            fixture.Window.UpdateLayout();
            var scroller = FindNamed<ScrollViewer>(fixture.View, "WeekScrollViewer");
            scroller.ScrollToVerticalOffset(scroller.ScrollableHeight);
            fixture.Window.UpdateLayout();
            var saturday = FindDescendants<Border>(fixture.View)
                .Where(border => border.Name == "WeekDaySection")
                .Last();
            var saturdayBottom = saturday.TranslatePoint(
                new Point(0, saturday.ActualHeight), fixture.View).Y;
            var overlayTop = overlay.TranslatePoint(new Point(), fixture.View).Y;

            Assert.True(saturdayBottom <= overlayTop + 0.5,
                $"Saturday bottom {saturdayBottom} should be above overlay top {overlayTop}.");
        });

    [Theory]
    [InlineData(109)]
    [InlineData(285)]
    public void MonthCanScrollFinalRowFullyAboveDayPanelAtOverlayBounds(double overlayHeight)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            var overlay = FindNamed<Grid>(fixture.View, "DayPanelOverlay");
            var expandedContent = FindNamed<Border>(fixture.View, "DayPanelExpandedContent");
            expandedContent.Height = overlayHeight - 17;
            fixture.Window.UpdateLayout();
            var scroller = FindNamed<ScrollViewer>(fixture.View, "MonthScrollViewer");
            scroller.ScrollToVerticalOffset(scroller.ScrollableHeight);
            fixture.Window.UpdateLayout();
            var monthItems = FindNamed<ItemsControl>(fixture.View, "MonthDayItems");
            var finalDay = FindDescendants<Button>(monthItems)
                .Where(button => button.DataContext is CalendarDayCellViewModel)
                .Last();
            var finalBottom = finalDay.TranslatePoint(
                new Point(0, finalDay.ActualHeight), fixture.View).Y;
            var overlayTop = overlay.TranslatePoint(new Point(), fixture.View).Y;

            Assert.True(scroller.ScrollableHeight >= overlayHeight - 0.5);
            Assert.True(finalBottom <= overlayTop + 0.5,
                $"Final month row bottom {finalBottom} should be above overlay top {overlayTop}.");
        });

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew, FlowDirection.RightToLeft)]
    [InlineData(CalendarLanguageMode.English, FlowDirection.LeftToRight)]
    public void EventActionsPopupUsesCompactLocalizedTextWithoutMirroringSurface(
        CalendarLanguageMode language,
        FlowDirection expectedTextFlow)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                language, width: 300, height: 458, entryCount: 1);
            var template = (DataTemplate)fixture.View.Resources["CalendarEventActionsPopupTemplate"];
            var popup = (FrameworkElement)template.LoadContent();
            popup.DataContext = new PopupContext(
                fixture.ViewModel,
                fixture.ViewModel.DayPanel.Entries.Single());
            popup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.DataBind,
                new Action(() => { }));
            var texts = FindDescendants<TextBlock>(popup).ToArray();

            Assert.Equal(100, popup.DesiredSize.Width, precision: 1);
            Assert.Equal(FlowDirection.LeftToRight, popup.FlowDirection);
            Assert.Equal(3, texts.Length);
            Assert.All(texts, text =>
            {
                Assert.Equal(expectedTextFlow, text.FlowDirection);
                Assert.Equal(TextAlignment.Left, text.TextAlignment);
            });
        });

    [Fact]
    public void SharedToolHeaderRemainsVisibleAndOpensOnSettingsPage()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            fixture.ViewModel.OpenSettingsCommand.Execute(null);
            fixture.Window.UpdateLayout();
            var header = FindNamed<FrameworkElement>(fixture.View, "CalendarToolHeader");
            var expanded = FindNamed<Border>(fixture.View, "CalendarHeaderExpandedContent");

            Assert.True(header.IsVisible);
            fixture.ViewModel.ToggleHeaderCommand.Execute(null);
            fixture.Window.UpdateLayout();
            Assert.True(fixture.ViewModel.IsSettingsPage);
            Assert.True(fixture.ViewModel.IsHeaderExpanded);
            Assert.Equal(Visibility.Visible, expanded.Visibility);
        });

    [Fact]
    public void WeekDoesNotRenderLocalizedNoEventsRows()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            fixture.ViewModel.ShowWeekCommand.Execute(null);
            fixture.Window.UpdateLayout();
            var weekItems = FindNamed<ItemsControl>(fixture.View, "WeekDayItems");

            Assert.DoesNotContain(FindDescendants<TextBlock>(weekItems), text =>
                text.Text is "No events" or "אין אירועים");
        });

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew, FlowDirection.RightToLeft)]
    [InlineData(CalendarLanguageMode.English, FlowDirection.LeftToRight)]
    public void HeaderSearchResultsUsePhysicalAlignmentForCalendarLanguage(
        CalendarLanguageMode language,
        FlowDirection expectedFlow)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                language, width: 300, height: 458, entryCount: 1);
            fixture.ViewModel.SearchText = "Entry";
            fixture.ViewModel.IsHeaderExpanded = true;
            fixture.Window.UpdateLayout();
            var button = FindNamed<Button>(fixture.View, "SearchResultButton");
            var content = FindNamed<StackPanel>(fixture.View, "SearchResultContent");
            var date = FindNamed<TextBlock>(fixture.View, "SearchResultDateText");
            var entry = FindNamed<TextBlock>(fixture.View, "SearchResultEventText");

            Assert.Equal(HorizontalAlignment.Stretch, button.HorizontalContentAlignment);
            Assert.Equal(expectedFlow, content.FlowDirection);
            Assert.All(new[] { date, entry }, text =>
            {
                Assert.Equal(expectedFlow, text.FlowDirection);
                Assert.Equal(TextAlignment.Left, text.TextAlignment);
                Assert.True(text.ActualWidth > button.ActualWidth / 2);
            });
        });

    [Fact]
    public void PeriodTitleCenterMatchesFullCalendarCenter()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(
                CalendarLanguageMode.English, width: 300, height: 458);
            var title = FindNamed<TextBlock>(fixture.View, "PeriodTitleText");
            var center = title.TranslatePoint(
                new Point(title.ActualWidth / 2, title.ActualHeight / 2), fixture.View).X;

            Assert.Equal(fixture.View.ActualWidth / 2, center, precision: 1);
        });

    private static DisplayedFixture CreateDisplayedFixture(
        CalendarLanguageMode language,
        double width,
        double height,
        int entryCount = 0)
    {
        var viewModel = CreateViewModel(language);
        viewModel.SelectDateCommand.Execute(Date);
        for (var index = 1; index <= entryCount; index++)
        {
            viewModel.BeginAddEventCommand.Execute(null);
            viewModel.EventDraftText = $"Entry {index}";
            viewModel.SaveEventCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        }

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
        window.Show();
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));
        window.UpdateLayout();
        return new DisplayedFixture(window, view, viewModel);
    }

    private static CalendarToolViewModel CreateViewModel(CalendarLanguageMode language) => new(
        new CalendarSettings
        {
            Language = language,
            FirstDayOfWeek = FirstDayOfWeekMode.Sunday
        },
        new CalendarLanguageResolver(() => new CultureInfo("en-US")),
        new HebrewCalendarHolidayProvider(),
        () => Date,
        new CultureInfo("en-US"));

    private static T FindNamed<T>(DependencyObject root, string name)
        where T : FrameworkElement => FindDescendants<T>(root)
            .Single(element => element.Name == name);

    private static void AssertSingleLineInput(TextBox textBox)
    {
        var contentHost = Assert.IsType<ScrollViewer>(
            textBox.Template.FindName("PART_ContentHost", textBox));
        Assert.Equal(30, textBox.ActualHeight, precision: 1);
        Assert.Equal(ScrollBarVisibility.Disabled,
            ScrollViewer.GetVerticalScrollBarVisibility(textBox));
        Assert.Equal(0, contentHost.ScrollableHeight, precision: 3);
        Assert.True(contentHost.ViewportHeight >= textBox.FontSize);
    }

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

    private sealed record PopupContext(
        CalendarToolViewModel Owner,
        CalendarEntryItemViewModel Entry);

    private sealed class DisplayedFixture : IDisposable
    {
        public DisplayedFixture(
            Window window,
            CalendarToolView view,
            CalendarToolViewModel viewModel)
        {
            Window = window;
            View = view;
            ViewModel = viewModel;
        }

        public Window Window { get; }

        public CalendarToolView View { get; }

        public CalendarToolViewModel ViewModel { get; }

        public void Dispose()
        {
            if (Window.IsLoaded)
            {
                Window.Close();
            }
        }
    }
}
