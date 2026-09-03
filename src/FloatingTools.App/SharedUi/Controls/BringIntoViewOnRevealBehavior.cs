using System.Windows;
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
                if (element.IsVisible)
                {
                    element.BringIntoView();
                }
            }));
    }
}
