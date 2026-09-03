using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FloatingTools.App.SharedUi.Controls;

/// <summary>
/// Plain read-only TextBox has no native "select paragraph" gesture on the third
/// click (that only exists for RichTextBox's real paragraph model), so this fills
/// the gap: a triple click selects the entire text of the TextBox it lands on.
/// </summary>
public static class TripleClickSelectAllBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TripleClickSelectAllBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void HandleClick(TextBoxBase textBox, MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(e);

        if (e.ClickCount != 3)
        {
            return;
        }

        textBox.SelectAll();
        e.Handled = true;
    }

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBoxBase textBox)
        {
            return;
        }

        if (e.NewValue is true)
        {
            textBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            textBox.Unloaded += OnUnloaded;
        }
        else
        {
            textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            textBox.Unloaded -= OnUnloaded;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBoxBase textBox)
        {
            HandleClick(textBox, e);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBoxBase textBox)
        {
            return;
        }

        textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        textBox.Unloaded -= OnUnloaded;
    }
}
