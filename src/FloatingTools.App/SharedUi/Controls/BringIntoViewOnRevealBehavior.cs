using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FloatingTools.App.SharedUi.Controls;

/// <summary>
/// Attached behavior that asks WPF to scroll a container into view after it
/// reveals additional content (e.g. an expanded Translation entry's action
/// row, or a newly-loaded alternative translation). Bind
/// <see cref="RevealVersionProperty"/> to an ever-incrementing counter that
/// the view model bumps only when new content becomes visible — never on
/// collapse. Each increase schedules a single <see cref="FrameworkElement.BringIntoView()"/>
/// call at <see cref="DispatcherPriority.Loaded"/>, which runs after the
/// current layout pass has measured/arranged the newly-revealed content, so
/// the resulting scroll (if any) is WPF's own minimal "just enough to be
/// visible" adjustment — never a forced scroll to top/center, and never a
/// no-op-defeating scroll when the content is already fully visible.
/// </summary>
public static class BringIntoViewOnRevealBehavior
{
    public static readonly DependencyProperty RevealVersionProperty =
        DependencyProperty.RegisterAttached(
            "RevealVersion",
            typeof(int),
            typeof(BringIntoViewOnRevealBehavior),
            new PropertyMetadata(0, OnRevealVersionChanged));

    public static int GetRevealVersion(DependencyObject element) =>
        (int)element.GetValue(RevealVersionProperty);

    public static void SetRevealVersion(DependencyObject element, int value) =>
        element.SetValue(RevealVersionProperty, value);

    private static void OnRevealVersionChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element
            || (int)e.NewValue <= (int)e.OldValue)
        {
            return;
        }

        element.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (!element.IsVisible)
                {
                    return;
                }

                var scrollViewer = FindScrollViewer(element);
                if (scrollViewer is { ViewportHeight: > 0 }
                    && element.ActualHeight > scrollViewer.ViewportHeight)
                {
                    // The full section cannot fit. Reveal its beginning and
                    // leave the remainder to normal feed scrolling.
                    element.BringIntoView(new Rect(
                        0, 0, element.ActualWidth, scrollViewer.ViewportHeight));
                }
                else
                {
                    element.BringIntoView();
                }
            }));
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element);
             parent is not null;
             parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer viewer)
            {
                return viewer;
            }
        }

        return null;
    }
}
