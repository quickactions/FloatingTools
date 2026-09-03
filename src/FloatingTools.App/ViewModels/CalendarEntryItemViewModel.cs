using System.Windows;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public sealed record CalendarEntryItemViewModel(
    Guid Id,
    DateOnly Date,
    string Text,
    bool IsSearchTarget = false)
{
    private TextDirectionResolution DirectionResolution =>
        TextDirectionResolver.Resolve(Text);

    public FlowDirection FlowDirection =>
        DirectionResolution.ToFlowDirection();

    public TextAlignment TextAlignment =>
        DirectionResolution.ToPhysicalTextAlignment();
}
