using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Views;
using FloatingTools.Tests.ViewModels;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class CalendarEventsStageTwoRuntimeTests
{
    [Theory]
    [InlineData(CalendarLanguageMode.English)]
    [InlineData(CalendarLanguageMode.Hebrew)]
    public void ResponsiveToolbarPreservesFilterAndSortAcrossSearchAndResize(CalendarLanguageMode language)
        => WpfTestApplication.Run(() =>
        {
            var vm = CalendarEventsStageTwoTests.CreateCalendar(language);
            vm.OpenEventsCommand.Execute(null);
            var events = vm.EventsViewModel!;
            var view = new CalendarToolView { DataContext = vm };
            var window = new Window { Content = view, Width = 300, Height = 458, Left = -10000,
                ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                Drain(window);
                var toolbar = (Grid)view.FindName("FullEventsToolbar");
                var search = (TextBox)view.FindName("FullEventsSearchTextBox");
                var icon = (Button)view.FindName("FullEventsSearchButton");
                var close = (Button)view.FindName("FullEventsCloseSearchButton");
                var filterControls = (Grid)view.FindName("FullEventsFilterControls");
                var filterLabel = (TextBlock)view.FindName("FullEventsFilterLabel");
                var preset = (ComboBox)view.FindName("FullEventsPresetComboBox");
                var sort = (Button)view.FindName("FullEventsSortButton");
                var divider = (Border)view.FindName("FullEventsToolbarDivider");
                var results = (ContentControl)view.FindName("FullEventsResults");
                AssertDividerPlacement(toolbar, divider, results);
                var labels = language == CalendarLanguageMode.Hebrew
                    ? new[] { "הכל", "מהיום", "השבוע הנוכחי", "החודש הנוכחי" }
                    : new[] { "All", "From today", "Current week", "Current month" };
                for (var i = 0; i < labels.Length; i++)
                {
                    preset.SelectedIndex = i;
                    Drain(window);
                    Assert.False(preset.IsDropDownOpen);
                    // Inspect the actual closed control, not its data item or popup.
                    var selectedText = Assert.Single(Descendants(preset).OfType<TextBlock>(),
                        text => text.IsVisible && !string.IsNullOrEmpty(text.Text));
                    Assert.Equal(labels[i], selectedText.Text);
                    Assert.Equal(FontWeights.Normal, selectedText.FontWeight);
                }
                Assert.True(icon.IsVisible);
                Assert.False(search.IsVisible);
                Assert.True(preset.IsVisible);
                Assert.True(sort.IsVisible);
                Assert.False(filterLabel.IsVisible);
                Assert.True(toolbar.ColumnDefinitions[0].Width.IsAuto);
                Assert.True(toolbar.ColumnDefinitions[1].Width.IsStar);
                Assert.True(toolbar.ColumnDefinitions[2].Width.IsAuto);
                Assert.Equal(5, filterControls.ColumnDefinitions.Count);
                Assert.True(filterControls.ColumnDefinitions[0].Width.IsAuto);
                Assert.True(filterControls.ColumnDefinitions[1].Width.IsStar);
                Assert.True(filterControls.ColumnDefinitions[2].Width.IsAuto);
                Assert.True(filterControls.ColumnDefinitions[3].Width.IsStar);
                Assert.Equal(new GridLength(30), filterControls.ColumnDefinitions[4].Width);
                Assert.Equal(filterControls.ColumnDefinitions[1].ActualWidth,
                    filterControls.ColumnDefinitions[3].ActualWidth, 2);
                var toolbarBounds = toolbar.TransformToAncestor(view).TransformBounds(new Rect(toolbar.RenderSize));
                var iconBounds = icon.TransformToAncestor(view).TransformBounds(new Rect(icon.RenderSize));
                var filterBounds = preset.TransformToAncestor(view).TransformBounds(new Rect(preset.RenderSize));
                var sortBounds = sort.TransformToAncestor(view).TransformBounds(new Rect(sort.RenderSize));
                var leftOuterGutter = sortBounds.Left - toolbarBounds.Left;
                var gapA = filterBounds.Left - sortBounds.Right;
                var gapB = iconBounds.Left - filterBounds.Right;
                var rightOuterGutter = toolbarBounds.Right - iconBounds.Right;
                Assert.True(sortBounds.Right < filterBounds.Left);
                Assert.True(filterBounds.Right < iconBounds.Left);
                Assert.InRange(leftOuterGutter, 3, 5);
                Assert.InRange(rightOuterGutter, 3, 5);
                Assert.InRange(Math.Abs(gapA - gapB), 0, 1);
                Assert.InRange(preset.ActualWidth, 135, 137);
                foreach (var control in new FrameworkElement[] { icon, preset, sort })
                {
                    var bounds = control.TransformToAncestor(toolbar).TransformBounds(new Rect(control.RenderSize));
                    Assert.True(bounds.Left >= -1 && bounds.Right <= toolbar.ActualWidth + 1);
                }
                Assert.Equal(4, preset.Items.Count);
                Assert.Empty(toolbar.RowDefinitions);
                Assert.Null(view.FindName("FullEventsFromTextBox"));
                Assert.Null(view.FindName("FullEventsAdvancedScroll"));
                Assert.Null(view.FindName("FullEventsResetButton"));
                preset.SelectedIndex = 1;
                sort.Command.Execute(null);
                icon.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Drain(window);
                Assert.True(icon.IsVisible);
                Assert.True(search.IsVisible);
                Assert.False(preset.IsVisible);
                Assert.False(sort.IsVisible);
                Assert.True(toolbar.ColumnDefinitions[0].Width.IsAuto);
                Assert.True(toolbar.ColumnDefinitions[1].Width.IsStar);
                Assert.True(toolbar.ColumnDefinitions[2].Width.IsAuto);
                Assert.True(search.ActualWidth > toolbar.ActualWidth * .8);
                AssertFilledControls(view, preset, search);
                AssertDividerPlacement(toolbar, divider, results);
                search.Text = "שלום";
                Assert.Equal("שלום", events.SearchText);
                close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Drain(window);
                Assert.False(search.IsVisible);
                Assert.True(preset.IsVisible);
                Assert.Equal(CalendarEventsPreset.FromToday, events.SelectedPreset);
                Assert.True(events.IsDescending);
                Assert.Equal("", events.SearchText);
                icon.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Drain(window);
                icon.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Drain(window);
                Assert.False(search.IsVisible);

                window.Width = 560;
                Drain(window);
                Assert.True(search.IsVisible);
                Assert.True(preset.IsVisible);
                Assert.True(sort.IsVisible);
                Assert.False(icon.IsVisible);
                Assert.True(filterLabel.IsVisible);
                Assert.True(toolbar.ColumnDefinitions[0].Width.IsAuto);
                Assert.True(toolbar.ColumnDefinitions[1].Width.IsStar);
                Assert.True(toolbar.ColumnDefinitions[2].Width.IsAuto);
                Assert.InRange(preset.ActualWidth, 135, 137);
                Assert.True(search.ActualWidth > 250);
                AssertDividerPlacement(toolbar, divider, results);
                foreach (var control in new FrameworkElement[] { search, preset, sort })
                {
                    var bounds = control.TransformToAncestor(toolbar).TransformBounds(new Rect(control.RenderSize));
                    Assert.True(bounds.Left >= -1 && bounds.Right <= toolbar.ActualWidth + 1);
                }
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    window.Resources.MergedDictionaries.Clear();
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"pack://application:,,,/FloatingTools.App;component/SharedUi/Tokens/Colors.{theme}.xaml")
                    });
                    Drain(window);
                    Assert.Equal(((SolidColorBrush)window.FindResource("FloatingToolsBrushForegroundPrimary")).Color,
                        Assert.IsType<SolidColorBrush>(search.Foreground).Color);
                    AssertFilledControls(view, preset, search);
                    Assert.Equal(((SolidColorBrush)window.FindResource("FloatingToolsBrushBorderDivider")).Color,
                        Assert.IsType<SolidColorBrush>(divider.Background).Color);
                }
                search.Text = "retain";
                window.Width = 300;
                Drain(window);
                Assert.True(search.IsVisible); // shrinking must not conceal an active query
                Assert.False(preset.IsVisible);
                close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                view.FocusSearch(); // existing Ctrl+F routing
                Drain(window);
                Assert.True(search.IsVisible);
                Assert.True(search.IsFocused);
                Assert.False(vm.IsHeaderExpanded);
            }
            finally { window.Close(); }
        });

    private static void AssertFilledControls(CalendarToolView view, ComboBox filter, TextBox search)
    {
        Assert.Same(view.FindResource("FullEventsFilledFilterStyle"), filter.Style);
        Assert.Same(view.FindResource("FullEventsFilledSearchStyle"), search.Style);
        foreach (var control in new Control[] { filter, search })
        {
            var fill = (SolidColorBrush)control.FindResource("FloatingToolsBrushSurfaceControl");
            Assert.Equal(fill.Color, Assert.IsType<SolidColorBrush>(control.Background).Color);
            Assert.NotEqual(((SolidColorBrush)control.FindResource("FloatingToolsBrushSurfaceBase")).Color, fill.Color);
            Assert.Equal(FontWeights.Normal, control.FontWeight);
            Assert.InRange(control.ActualHeight, 29, 31);
        }
        var inputSurface = (Border)search.Template.FindName("InputSurface", search);
        Assert.Equal(((SolidColorBrush)search.Background).Color, ((SolidColorBrush)inputSurface.Background).Color);
        Assert.True(inputSurface.CornerRadius.TopLeft > 0);
        Assert.Equal(0, ((SolidColorBrush)filter.BorderBrush).Color.A);
    }

    private static void AssertDividerPlacement(Grid toolbar, Border divider, ContentControl results)
    {
        var parent = (StackPanel)toolbar.Parent;
        var index = parent.Children.IndexOf(toolbar);
        Assert.Same(divider, parent.Children[index + 1]);
        Assert.Same(results, parent.Children[index + 2]);
        Assert.True(divider.IsVisible);
        Assert.InRange(divider.ActualHeight, .5, 1.5);
        Assert.InRange(Math.Abs(divider.ActualWidth - toolbar.ActualWidth), 0, 1);
        Assert.True(divider.TranslatePoint(new Point(), parent).Y >=
            toolbar.TranslatePoint(new Point(0, toolbar.ActualHeight), parent).Y);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Drain(Window window)
    {
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }
}
