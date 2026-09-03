using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.Services;

public sealed class PanelDragVisibilitySessionTests
{
    [Theory]
    [InlineData(PanelState.ActiveTool)]
    [InlineData(PanelState.ToolMenu)]
    [InlineData(PanelState.ApplicationSettings)]
    public void Begin_OpenPanel_SchedulesSameContentForRestore(PanelState panelState)
    {
        var session = new PanelDragVisibilitySession();

        var shouldHide = session.Begin(panelState);

        Assert.True(shouldHide);
        Assert.True(session.RestorePending);
        Assert.Equal(panelState, session.PreservedPanelState);
    }

    [Fact]
    public void Begin_ClosedPanel_DoesNotScheduleRestore()
    {
        var session = new PanelDragVisibilitySession();

        var shouldHide = session.Begin(PanelState.Closed);

        Assert.False(shouldHide);
        Assert.False(session.RestorePending);
        Assert.Null(session.PreservedPanelState);
    }

    [Fact]
    public void Begin_DoesNotChangeViewModelStateOrTool()
    {
        var viewModel = new FloatingToolbarViewModel(ToolId.Translation)
        {
            PanelState = PanelState.ActiveTool
        };
        var session = new PanelDragVisibilitySession();

        session.Begin(viewModel.PanelState);

        Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
        Assert.Equal(ToolId.Translation, viewModel.ActiveTool);
        Assert.Equal(ToolId.Translation, viewModel.LastUsedTool);
    }

    [Theory]
    [InlineData(PanelState.ActiveTool)]
    [InlineData(PanelState.ToolMenu)]
    [InlineData(PanelState.ApplicationSettings)]
    public void Complete_OpenPanel_RestoresOnce(PanelState panelState)
    {
        var session = new PanelDragVisibilitySession();
        session.Begin(panelState);

        var firstCompletion = session.Complete(panelState);
        var secondCompletion = session.Complete(panelState);

        Assert.True(firstCompletion);
        Assert.False(secondCompletion);
        Assert.False(session.IsDragActive);
    }

    [Fact]
    public void Complete_PanelClosedDuringDrag_DoesNotRestore()
    {
        var session = new PanelDragVisibilitySession();
        session.Begin(PanelState.ActiveTool);
        session.PanelClosed();

        var shouldRestore = session.Complete(PanelState.Closed);

        Assert.False(shouldRestore);
    }

    [Fact]
    public void Begin_WhenAlreadyActive_DoesNotStartSecondCycle()
    {
        var session = new PanelDragVisibilitySession();
        session.Begin(PanelState.ToolMenu);

        var secondBegin = session.Begin(PanelState.ActiveTool);

        Assert.False(secondBegin);
        Assert.Equal(PanelState.ToolMenu, session.PreservedPanelState);
    }

    [Fact]
    public void Cancel_ClearsPendingRestore()
    {
        var session = new PanelDragVisibilitySession();
        session.Begin(PanelState.ActiveTool);

        session.Cancel();

        Assert.False(session.IsDragActive);
        Assert.False(session.RestorePending);
        Assert.False(session.Complete(PanelState.ActiveTool));
    }

    [Fact]
    public void Complete_WhileClosing_DoesNotRestore()
    {
        var session = new PanelDragVisibilitySession();
        session.Begin(PanelState.ActiveTool);

        var shouldRestore = session.Complete(
            PanelState.ActiveTool,
            isClosing: true);

        Assert.False(shouldRestore);
    }
}
