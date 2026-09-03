using System.Windows;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public sealed class AlternativeTranslationViewModel
{
    public AlternativeTranslationViewModel(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    public string Text { get; }

    private TextDirectionResolution DirectionResolution =>
        TextDirectionResolver.Resolve(Text);

    public FlowDirection FlowDirection =>
        DirectionResolution.ToFlowDirection();

    public TextAlignment TextAlignment =>
        DirectionResolution.ToPhysicalTextAlignment();
}
