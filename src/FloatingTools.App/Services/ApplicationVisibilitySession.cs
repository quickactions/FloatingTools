namespace FloatingTools.App.Services;

/// <summary>
/// Tracks whether the global Show/Hide shortcut has hidden the app, and
/// whether the panel was open at the moment it was hidden, so showing again
/// can restore exactly what was visible before without touching PanelState.
/// </summary>
public sealed class ApplicationVisibilitySession
{
    public bool IsHidden { get; private set; }

    public bool PanelWasVisibleBeforeHide { get; private set; }

    /// <summary>Returns false (no-op) if already hidden.</summary>
    public bool Hide(bool isPanelCurrentlyVisible)
    {
        if (IsHidden)
        {
            return false;
        }

        PanelWasVisibleBeforeHide = isPanelCurrentlyVisible;
        IsHidden = true;
        return true;
    }

    /// <summary>Returns false (no-op) if not currently hidden.</summary>
    public bool Show()
    {
        if (!IsHidden)
        {
            return false;
        }

        IsHidden = false;
        return true;
    }
}
