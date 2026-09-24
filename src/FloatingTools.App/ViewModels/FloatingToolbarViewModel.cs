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

    private PanelSizePreset _activeToolPanelSize;
    private double _standardPanelZoomPercentage;
    private double _largePanelZoomPercentage;
    private double _effectivePanelZoomPercentage;
    private Func<double, bool>? _canApplyZoomPercentage;

    public FloatingToolbarViewModel(
        ToolId lastUsedTool = ToolId.Translation,
        PanelSizePreset activeToolPanelSize = PanelSizePreset.Standard,
        double panelZoomPercentage = PanelZoomCalculator.DefaultPercentage,
        double largePanelZoomPercentage = PanelZoomCalculator.DefaultPercentage)
    {
        _lastUsedTool = NormalizeTool(lastUsedTool);
        _activeToolPanelSize = PanelSizeCalculator.NormalizePreset(activeToolPanelSize);
        _standardPanelZoomPercentage = PanelZoomCalculator.NormalizePercentage(
            PanelSizePreset.Standard,
            panelZoomPercentage);
        _largePanelZoomPercentage = PanelZoomCalculator.NormalizePercentage(
            PanelSizePreset.Large,
            largePanelZoomPercentage);
        _effectivePanelZoomPercentage = PanelZoomPercentage;
    }

    public PanelSizePreset ActiveToolPanelSize
    {
        get => _activeToolPanelSize;
        private set
        {
            var normalized = PanelSizeCalculator.NormalizePreset(value);
            if (SetProperty(ref _activeToolPanelSize, normalized))
            {
                OnPropertyChanged(nameof(PanelZoomPercentage));
            }
        }
    }

    public double PanelZoomPercentage =>
        ActiveToolPanelSize == PanelSizePreset.Large
            ? _largePanelZoomPercentage
            : _standardPanelZoomPercentage;

    public double StandardPanelZoomPercentage => _standardPanelZoomPercentage;

    public double LargePanelZoomPercentage => _largePanelZoomPercentage;

    public ToolId ActiveTool => LastUsedTool;

    public string LastUsedToolName => GetToolName(LastUsedTool);

    public string ActiveToolName => GetToolName(ActiveTool);

    public string ActivePanelTitle => PanelState == PanelState.ApplicationSettings
        ? "FloatingTools Settings"
        : ActiveToolName;

    public void UpdateZoomContext(
        double effectivePercentage,
        Func<double, bool> canApplyZoomPercentage)
    {
        ArgumentNullException.ThrowIfNull(canApplyZoomPercentage);
        _effectivePanelZoomPercentage = double.IsFinite(effectivePercentage)
            ? effectivePercentage
            : PanelZoomPercentage;
        _canApplyZoomPercentage = canApplyZoomPercentage;
    }

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
    private void ClosePanel() => PanelState = PanelState.Closed;

    [RelayCommand]
    private void OpenApplicationSettings() =>
        PanelState = PanelState.ApplicationSettings;

    [RelayCommand]
    private void SelectTool(ToolId tool)
    {
        LastUsedTool = NormalizeTool(tool);
        PanelState = PanelState.ActiveTool;
    }

    [RelayCommand]
    private void SelectPanelSize(PanelSizePreset preset) =>
        ActiveToolPanelSize = PanelSizeCalculator.NormalizePreset(preset);

    [RelayCommand]
    private void ZoomIn()
    {
        if (PanelZoomPercentage > _effectivePanelZoomPercentage + PanelZoomCalculator.PercentageStepTolerance)
        {
            return;
        }

        SetPanelZoomPercentage(
            PanelZoomCalculator.Increase(
                ActiveToolPanelSize,
                PanelZoomPercentage));
    }

    [RelayCommand]
    private void ZoomOut() =>
        SetPanelZoomPercentage(
            PanelZoomCalculator.DecreaseFromEffective(
                ActiveToolPanelSize,
                _effectivePanelZoomPercentage));

    [RelayCommand]
    private void ResetZoom() =>
        SetPanelZoomPercentage(PanelZoomCalculator.DefaultPercentage);

    private void SetPanelZoomPercentage(double percentage)
    {
        var normalized = PanelZoomCalculator.NormalizePercentage(
            ActiveToolPanelSize,
            percentage);
        if (normalized == PanelZoomPercentage
            || (_canApplyZoomPercentage is not null
                && !_canApplyZoomPercentage(normalized)))
        {
            return;
        }

        if (ActiveToolPanelSize == PanelSizePreset.Large)
        {
            SetProperty(
                ref _largePanelZoomPercentage,
                normalized,
                nameof(LargePanelZoomPercentage));
        }
        else
        {
            SetProperty(
                ref _standardPanelZoomPercentage,
                normalized,
                nameof(StandardPanelZoomPercentage));
        }

        _effectivePanelZoomPercentage = normalized;
        OnPropertyChanged(nameof(PanelZoomPercentage));
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
