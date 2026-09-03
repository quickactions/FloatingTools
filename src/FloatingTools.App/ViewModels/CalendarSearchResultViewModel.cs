using System.Windows;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public sealed record CalendarSearchResultViewModel(
    Guid EntryId,
    DateOnly Date,
    string DateTitle,
    string Text)
{
    private TextDirectionResolution DirectionResolution =>
        TextDirectionResolver.Resolve(Text);

    public FlowDirection TextFlowDirection =>
        DirectionResolution.ToFlowDirection();

    public TextAlignment TextAlignment =>
        DirectionResolution.ToPhysicalTextAlignment();
}
