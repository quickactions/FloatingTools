using System.Windows;

namespace FloatingTools.App.Controls;

/// <summary>
/// Watermark text for inputs templated by FloatingToolsInputBaseStyle. The
/// watermark is a child of the control template beside PART_ContentHost and
/// shares its Margin="{TemplateBinding Padding}", so its inset can never drift
/// from the editable text geometry the way an independently positioned sibling
/// overlay does.
/// </summary>
public static class TextBoxWatermark
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(TextBoxWatermark),
            new FrameworkPropertyMetadata(null));

    public static string? GetText(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.GetValue(TextProperty);
    }

    public static void SetText(DependencyObject element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(TextProperty, value);
    }
}
