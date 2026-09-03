using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class PanelDragVisibilitySession
{
    public bool IsDragActive { get; private set; }

    public bool RestorePending { get; private set; }

    public PanelState? PreservedPanelState { get; private set; }

    public bool Begin(PanelState panelState)
    {
        if (IsDragActive)
        {
            return false;
        }

        IsDragActive = true;
        RestorePending = panelState != PanelState.Closed;
        PreservedPanelState = RestorePending ? panelState : null;
        return RestorePending;
    }

    public bool Complete(PanelState currentPanelState, bool isClosing = false)
    {
        if (!IsDragActive)
        {
            return false;
        }

        var shouldRestore = RestorePending
            && !isClosing
            && currentPanelState != PanelState.Closed;
        Reset();
        return shouldRestore;
    }

    public void PanelClosed()
    {
        RestorePending = false;
        PreservedPanelState = null;
    }

    public void Cancel() => Reset();

    private void Reset()
    {
        IsDragActive = false;
        RestorePending = false;
        PreservedPanelState = null;
    }
}
