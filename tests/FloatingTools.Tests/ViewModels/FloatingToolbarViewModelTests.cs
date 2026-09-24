using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class FloatingToolbarViewModelTests
{
    [Fact]
    public void MainIcon_FromClosed_OpensActiveTool()
    {
        var viewModel = CreateViewModel(PanelState.Closed);

        viewModel.ToggleActiveToolPanelCommand.Execute(null);

        Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
        Assert.Equal(ToolId.Translation, viewModel.ActiveTool);
    }

    [Fact]
    public void ToolsButton_FromClosed_OpensToolMenu()
    {
        var viewModel = CreateViewModel(PanelState.Closed);

        viewModel.ToggleToolMenuCommand.Execute(null);

        Assert.Equal(PanelState.ToolMenu, viewModel.PanelState);
    }

    [Fact]
    public void MainIcon_FromActiveTool_ClosesPanel()
    {
        var viewModel = CreateViewModel(PanelState.ActiveTool);

        viewModel.ToggleActiveToolPanelCommand.Execute(null);

        Assert.Equal(PanelState.Closed, viewModel.PanelState);
    }

    [Fact]
    public void ToolsButton_FromActiveTool_OpensToolMenu()
    {
        var viewModel = CreateViewModel(PanelState.ActiveTool);

        viewModel.ToggleToolMenuCommand.Execute(null);

        Assert.Equal(PanelState.ToolMenu, viewModel.PanelState);
    }

    [Fact]
    public void ToolsButton_FromToolMenu_ClosesPanel()
    {
        var viewModel = CreateViewModel(PanelState.ToolMenu);

        viewModel.ToggleToolMenuCommand.Execute(null);

        Assert.Equal(PanelState.Closed, viewModel.PanelState);
    }

    [Fact]
    public void MainIcon_FromToolMenu_OpensLastUsedTool()
    {
        var viewModel = CreateViewModel(PanelState.ToolMenu);

        viewModel.ToggleActiveToolPanelCommand.Execute(null);

        Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
        Assert.Equal(viewModel.LastUsedTool, viewModel.ActiveTool);
    }

    [Fact]
    public void SelectingTranslation_FromToolMenu_UpdatesToolAndOpensIt()
    {
        var viewModel = CreateViewModel(PanelState.ToolMenu);

        viewModel.SelectToolCommand.Execute(ToolId.Translation);

        Assert.Equal(ToolId.Translation, viewModel.LastUsedTool);
        Assert.Equal(ToolId.Translation, viewModel.ActiveTool);
        Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
    }

    [Fact]
    public void SelectingNotes_FromToolMenu_UpdatesToolAndOpensIt()
    {
        var viewModel = CreateViewModel(PanelState.ToolMenu);

        viewModel.SelectToolCommand.Execute(ToolId.Notes);

        Assert.Equal(ToolId.Notes, viewModel.ActiveTool);
        Assert.Equal("Notes", viewModel.ActiveToolName);
        Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
    }

    [Fact]
    public void SelectingCalendar_FromToolMenu_UpdatesToolAndOpensIt()
    {
        var viewModel = CreateViewModel(PanelState.ToolMenu);

        viewModel.SelectToolCommand.Execute(ToolId.Calendar);

        Assert.Equal(ToolId.Calendar, viewModel.ActiveTool);
        Assert.Equal("Calendar", viewModel.ActiveToolName);
        Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
    }

    [Fact]
    public void InvalidLastUsedTool_FallsBackToTranslation()
    {
        var viewModel = new FloatingToolbarViewModel((ToolId)999);

        Assert.Equal(ToolId.Translation, viewModel.LastUsedTool);
        Assert.Equal(ToolId.Translation, viewModel.ActiveTool);
        Assert.Equal("Translation", viewModel.LastUsedToolName);
    }

    [Fact]
    public void ClosePanelCommand_ClosesEitherPanel()
    {
        var viewModel = CreateViewModel(PanelState.ToolMenu);

        viewModel.ClosePanelCommand.Execute(null);

        Assert.Equal(PanelState.Closed, viewModel.PanelState);
    }

    [Fact]
    public void ApplicationSettings_UsesNonToolPanelStateWithoutChangingLastTool()
    {
        var viewModel = new FloatingToolbarViewModel(ToolId.Notes);

        viewModel.OpenApplicationSettingsCommand.Execute(null);

        Assert.Equal(PanelState.ApplicationSettings, viewModel.PanelState);
        Assert.Equal("FloatingTools Settings", viewModel.ActivePanelTitle);
        Assert.Equal(ToolId.Notes, viewModel.LastUsedTool);
        Assert.Equal(ToolId.Notes, viewModel.ActiveTool);
    }

    [Fact]
    public void ApplicationSettings_CloseAndReopen_RestoresTheSettingsPanel()
    {
        var viewModel = new FloatingToolbarViewModel();

        viewModel.OpenApplicationSettingsCommand.Execute(null);
        viewModel.ClosePanelCommand.Execute(null);
        viewModel.OpenApplicationSettingsCommand.Execute(null);

        Assert.Equal(PanelState.ApplicationSettings, viewModel.PanelState);
    }

    [Fact]
    public void PanelSize_DefaultsToStandard()
    {
        var viewModel = new FloatingToolbarViewModel();

        Assert.Equal(PanelSizePreset.Standard, viewModel.ActiveToolPanelSize);
    }

    [Fact]
    public void SelectPanelSize_ChangesActiveToolPreset()
    {
        var viewModel = new FloatingToolbarViewModel();

        viewModel.SelectPanelSizeCommand.Execute(PanelSizePreset.Large);

        Assert.Equal(PanelSizePreset.Large, viewModel.ActiveToolPanelSize);
    }

    [Fact]
    public void InvalidPanelSize_FallsBackToStandard()
    {
        var viewModel = new FloatingToolbarViewModel(
            ToolId.Translation,
            (PanelSizePreset)999);

        Assert.Equal(PanelSizePreset.Standard, viewModel.ActiveToolPanelSize);
    }

    [Fact]
    public void PanelZoom_DefaultsToOneHundredPercentForBothPresets()
    {
        var viewModel = new FloatingToolbarViewModel();

        Assert.Equal(100, viewModel.StandardPanelZoomPercentage);
        Assert.Equal(100, viewModel.LargePanelZoomPercentage);
        Assert.Equal(100, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void StandardZoomCommands_UseTenPointStepsAndEightyToOneFortyLimits()
    {
        var viewModel = new FloatingToolbarViewModel();

        for (var index = 0; index < 10; index++)
        {
            viewModel.ZoomInCommand.Execute(null);
        }
        Assert.Equal(140, viewModel.PanelZoomPercentage);

        for (var index = 0; index < 10; index++)
        {
            viewModel.ZoomOutCommand.Execute(null);
        }
        Assert.Equal(80, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void LargeZoomCommands_UseTenPointStepsAndEightyToOneHundredLimits()
    {
        var viewModel = new FloatingToolbarViewModel(
            activeToolPanelSize: PanelSizePreset.Large);

        for (var index = 0; index < 10; index++)
        {
            viewModel.ZoomInCommand.Execute(null);
        }
        Assert.Equal(100, viewModel.PanelZoomPercentage);

        viewModel.ZoomOutCommand.Execute(null);
        Assert.Equal(90, viewModel.PanelZoomPercentage);
        viewModel.ZoomOutCommand.Execute(null);
        viewModel.ZoomOutCommand.Execute(null);
        Assert.Equal(80, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void PresetSwitchesRestoreIndependentZoomPreferences()
    {
        var viewModel = new FloatingToolbarViewModel(
            panelZoomPercentage: 130,
            largePanelZoomPercentage: 90);

        Assert.Equal(130, viewModel.PanelZoomPercentage);
        viewModel.SelectPanelSizeCommand.Execute(PanelSizePreset.Large);
        Assert.Equal(90, viewModel.PanelZoomPercentage);
        viewModel.SelectPanelSizeCommand.Execute(PanelSizePreset.Standard);
        Assert.Equal(130, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void RepeatedZoomInAtLargeMaximumDoesNotAccumulateHiddenSteps()
    {
        var viewModel = new FloatingToolbarViewModel(
            activeToolPanelSize: PanelSizePreset.Large);

        viewModel.ZoomInCommand.Execute(null);
        viewModel.ZoomInCommand.Execute(null);
        viewModel.ZoomOutCommand.Execute(null);

        Assert.Equal(90, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void ZoomInThenOut_UsesCalculatedEffectivePercentageAtOneHundredTen()
    {
        var viewModel = new FloatingToolbarViewModel(panelZoomPercentage: 100);

        viewModel.ZoomInCommand.Execute(null);
        Assert.Equal(110, viewModel.PanelZoomPercentage);

        var layout = PanelZoomCalculator.CalculateLayout(
            PanelSizePreset.Standard,
            viewModel.PanelZoomPercentage,
            availableWidthDip: 2000,
            availableHeightDip: 2000);
        viewModel.UpdateZoomContext(layout.EffectivePercentage, _ => true);
        viewModel.ZoomOutCommand.Execute(null);

        Assert.Equal(100, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void FirstZoomOutFromMonitorCappedPreferenceChangesVisibleStep()
    {
        var viewModel = new FloatingToolbarViewModel(panelZoomPercentage: 140);
        viewModel.UpdateZoomContext(113, _ => true);

        viewModel.ZoomOutCommand.Execute(null);

        Assert.Equal(110, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void MonitorCapDoesNotOverwriteSavedPreference()
    {
        var viewModel = new FloatingToolbarViewModel(panelZoomPercentage: 140);

        viewModel.UpdateZoomContext(113, _ => true);

        Assert.Equal(140, viewModel.StandardPanelZoomPercentage);
        Assert.Equal(140, viewModel.PanelZoomPercentage);
    }

    [Fact]
    public void InvisibleZoomCommandsDoNotChangePreference()
    {
        var viewModel = new FloatingToolbarViewModel(panelZoomPercentage: 100);
        viewModel.UpdateZoomContext(100, _ => false);

        viewModel.ZoomInCommand.Execute(null);
        viewModel.ZoomOutCommand.Execute(null);
        viewModel.ResetZoomCommand.Execute(null);

        Assert.Equal(100, viewModel.PanelZoomPercentage);
    }

    [Theory]
    [InlineData(PanelSizePreset.Standard, 130)]
    [InlineData(PanelSizePreset.Large, 90)]
    public void ResetZoom_ReturnsActivePresetToOneHundredPercent(
        PanelSizePreset preset,
        double initial)
    {
        var viewModel = new FloatingToolbarViewModel(
            activeToolPanelSize: preset,
            panelZoomPercentage: preset == PanelSizePreset.Standard ? initial : 100,
            largePanelZoomPercentage: preset == PanelSizePreset.Large ? initial : 100);

        viewModel.ResetZoomCommand.Execute(null);

        Assert.Equal(100, viewModel.PanelZoomPercentage);
    }

    [Theory]
    [InlineData(double.NaN, 100)]
    [InlineData(double.PositiveInfinity, 100)]
    [InlineData(10, 80)]
    [InlineData(500, 140)]
    [InlineData(114, 110)]
    public void StandardZoom_ConstructorNormalizesInvalidValues(
        double value,
        double expected)
    {
        var viewModel = new FloatingToolbarViewModel(
            panelZoomPercentage: value);

        Assert.Equal(expected, viewModel.StandardPanelZoomPercentage);
    }

    [Theory]
    [InlineData(double.NaN, 100)]
    [InlineData(10, 80)]
    [InlineData(500, 100)]
    [InlineData(94, 90)]
    public void LargeZoom_ConstructorNormalizesInvalidValues(
        double value,
        double expected)
    {
        var viewModel = new FloatingToolbarViewModel(
            largePanelZoomPercentage: value);

        Assert.Equal(expected, viewModel.LargePanelZoomPercentage);
    }
    private static FloatingToolbarViewModel CreateViewModel(PanelState panelState)
    {
        var viewModel = new FloatingToolbarViewModel(ToolId.Translation)
        {
            PanelState = panelState
        };
        return viewModel;
    }
}
