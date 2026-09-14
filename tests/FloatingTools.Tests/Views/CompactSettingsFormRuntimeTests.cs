using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class CompactSettingsFormRuntimeTests
{
    [Theory]
    [InlineData(FlowDirection.LeftToRight, 300)]
    [InlineData(FlowDirection.RightToLeft, 300)]
    [InlineData(FlowDirection.LeftToRight, 560)]
    [InlineData(FlowDirection.RightToLeft, 560)]
    public void ApplicationSelectsStayCompactAndAlignWithTheirSection(FlowDirection flow, double width)
        => WpfTestApplication.Run(() =>
        {
            var view = new ApplicationSettingsView { FlowDirection = flow };
            var window = new Window { Content = view, Width = width, Height = 850, Left = -10000,
                ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var combos = Descendants(view).OfType<ComboBox>().ToArray();
                Assert.Equal(3, combos.Length); // Language, Appearance, Default Model
                foreach (var combo in combos)
                {
                    // Application Settings currently has an English shell. Exercise the
                    // reusable style with an explicitly RTL section as Calendar uses.
                    ((StackPanel)combo.Parent).FlowDirection = flow;
                    combo.ItemsSource = new[] { new { DisplayName = "System / Default model" } };
                    combo.SelectedIndex = 0;
                }
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                foreach (var combo in combos)
                {
                    var parent = (StackPanel)combo.Parent;
                    Assert.InRange(combo.ActualWidth, Math.Min(300, parent.ActualWidth) - 1, Math.Min(300, parent.ActualWidth) + 1);
                    var bounds = combo.TransformToAncestor(parent).TransformBounds(new Rect(combo.RenderSize));
                    Assert.InRange(bounds.Left, -1, 1); // logical leading edge; parent mirrors in RTL
                    Assert.Equal(flow, combo.FlowDirection);
                    Assert.True(bounds.Right <= parent.ActualWidth + 1);
                }
            }
            finally { window.Close(); }
        });

    [Theory]
    [InlineData(FloatingTools.App.Models.CalendarLanguageMode.English)]
    [InlineData(FloatingTools.App.Models.CalendarLanguageMode.Hebrew)]
    public void CalendarSettingsUseActualLocalizedSectionDirection(FloatingTools.App.Models.CalendarLanguageMode language)
        => WpfTestApplication.Run(() =>
        {
            var vm = FloatingTools.Tests.ViewModels.CalendarEventsStageTwoTests.CreateCalendar(language);
            vm.OpenSettingsCommand.Execute(null);
            var view = new CalendarToolView { DataContext = vm };
            var window = new Window { Content = view, Width = 560, Height = 600, Left = -10000,
                ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                foreach (var name in new[] { "CalendarLanguageComboBox", "CalendarDefaultViewComboBox", "CalendarFirstDayComboBox" })
                {
                    var combo = (ComboBox)view.FindName(name);
                    Assert.InRange(combo.ActualWidth, 299, 301);
                    Assert.Equal(vm.ContentFlowDirection, combo.FlowDirection);
                    Assert.InRange(combo.TranslatePoint(new Point(), (UIElement)combo.Parent).X, -1, 1);
                }
            }
            finally { window.Close(); }
        });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
