using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public partial class CalendarEventListItemViewModel(
    CalendarEntry entry,
    CalendarEventPreview preview,
    string dateText) : ObservableObject
{
    public Guid Id { get; } = entry.Id;
    public DateOnly Date { get; } = entry.Date;
    public string Text { get; } = entry.Text;
    public string Preview { get; } = preview.Text;
    public bool HasHiddenContent { get; } = preview.IsTruncated;
    public string DateText { get; } = dateText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollapsed))]
    private bool _isExpanded;

    public bool IsCollapsed => !IsExpanded;

    private TextDirectionResolution DirectionResolution =>
        TextDirectionResolver.Resolve(Text);

    public FlowDirection TextFlowDirection => DirectionResolution.ToFlowDirection();
    public TextAlignment TextAlignment => DirectionResolution.ToPhysicalTextAlignment();

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void ToggleExpanded()
    {
        if (HasHiddenContent)
        {
            IsExpanded = !IsExpanded;
        }
    }

    private bool CanToggleExpanded() => HasHiddenContent;
}
