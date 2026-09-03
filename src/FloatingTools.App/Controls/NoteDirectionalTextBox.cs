using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.Controls;

public sealed class NoteDirectionalTextBox : TextBox
{
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);

        var resolution = TextDirectionResolver.Resolve(Text);
        // Notes historically presents neutral input (numbers, punctuation, and
        // empty text) as LTR/left. Keep that visual fallback at the consumer.
        SetCurrentValue(
            FlowDirectionProperty,
            resolution.Direction == TextDirection.RightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight);
        SetCurrentValue(
            TextAlignmentProperty,
            resolution.Alignment == TextDirectionAlignment.Right
                ? TextAlignment.Right
                : TextAlignment.Left);
    }
}
