using FloatingTools.App.Models;
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

    private static FloatingToolbarViewModel CreateViewModel(PanelState panelState)
    {
        var viewModel = new FloatingToolbarViewModel(ToolId.Translation)
        {
            PanelState = panelState
        };
        return viewModel;
    }
}
