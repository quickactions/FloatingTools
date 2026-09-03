using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.ViewModels;

public partial class FloatingToolbarViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePanelTitle))]
    private PanelState _panelState = PanelState.Closed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveTool))]
    [NotifyPropertyChangedFor(nameof(LastUsedToolName))]
    [NotifyPropertyChangedFor(nameof(ActiveToolName))]
    [NotifyPropertyChangedFor(nameof(ActivePanelTitle))]
    private ToolId _lastUsedTool;

    [ObservableProperty]
    private PanelSizePreset _activeToolPanelSize;

    public FloatingToolbarViewModel(
        ToolId lastUsedTool = ToolId.Translation,
        PanelSizePreset activeToolPanelSize = PanelSizePreset.Standard)
    {
        _lastUsedTool = NormalizeTool(lastUsedTool);
        _activeToolPanelSize = PanelSizeCalculator.NormalizePreset(activeToolPanelSize);
    }

    public ToolId ActiveTool => LastUsedTool;

    public string LastUsedToolName => GetToolName(LastUsedTool);

    public string ActiveToolName => GetToolName(ActiveTool);

    public string ActivePanelTitle => PanelState == PanelState.ApplicationSettings
        ? "FloatingTools Settings"
        : ActiveToolName;

    [RelayCommand]
    private void ToggleActiveToolPanel()
    {
        PanelState = PanelState == PanelState.ActiveTool
            ? PanelState.Closed
            : PanelState.ActiveTool;
    }

    [RelayCommand]
    private void ToggleToolMenu()
    {
        PanelState = PanelState == PanelState.ToolMenu
            ? PanelState.Closed
            : PanelState.ToolMenu;
    }

    [RelayCommand]
    private void ClosePanel()
    {
        PanelState = PanelState.Closed;
    }

    [RelayCommand]
    private void OpenApplicationSettings()
    {
        PanelState = PanelState.ApplicationSettings;
    }

    [RelayCommand]
    private void SelectTool(ToolId tool)
    {
        LastUsedTool = NormalizeTool(tool);
        PanelState = PanelState.ActiveTool;
    }

    [RelayCommand]
    private void SelectPanelSize(PanelSizePreset preset)
    {
        ActiveToolPanelSize = PanelSizeCalculator.NormalizePreset(preset);
    }

    private static ToolId NormalizeTool(ToolId tool) =>
        Enum.IsDefined(tool) ? tool : ToolId.Translation;

    private static string GetToolName(ToolId tool) => NormalizeTool(tool) switch
    {
        ToolId.Translation => "Translation",
        ToolId.Notes => "Notes",
        ToolId.QuickChat => "Quick Chat",
        ToolId.Calendar => "Calendar",
        _ => "Translation"
    };
}
