using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.Controls;

public sealed class DirectionalTextBox : TextBox
{
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);

        var resolution = TextDirectionResolver.Resolve(Text);
        SetCurrentValue(FlowDirectionProperty,
            resolution.Direction == TextDirection.RightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight);
        SetCurrentValue(TextAlignmentProperty,
            resolution.Alignment == TextDirectionAlignment.Right
                ? TextAlignment.Right
                : TextAlignment.Left);
        InvalidateMeasure();
    }
}
