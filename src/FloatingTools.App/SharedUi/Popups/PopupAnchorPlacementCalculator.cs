using System.Windows;

namespace FloatingTools.App.SharedUi.Popups;

public readonly record struct PopupAnchorPlacement(
    Point Position,
    PopupAnchorPreferredPlacement Placement);

/// <summary>
/// Calculates in the clamp element's local device-independent coordinate space.
/// Horizontal placement is centered and clamped. Vertical placement has exactly
/// two outcomes: anchor.Bottom + gap or anchor.Top - gap - popup.Height.
/// </summary>
public static class PopupAnchorPlacementCalculator
{
    public static PopupAnchorPlacement Calculate(
        Rect anchorBounds,
        Size popupSize,
        Rect clampBounds,
        PopupAnchorPreferredPlacement preferredPlacement
            = PopupAnchorPreferredPlacement.Below,
        double gap = 4,
        double edgeMargin = 5)
    {
        Validate(anchorBounds, popupSize, clampBounds, gap, edgeMargin);

        var minimumX = clampBounds.Left + edgeMargin;
        var maximumX = Math.Max(
            minimumX,
            clampBounds.Right - popupSize.Width - edgeMargin);
        var preferredX = anchorBounds.Left
            + (anchorBounds.Width / 2)
            - (popupSize.Width / 2);
        var x = Math.Clamp(preferredX, minimumX, maximumX);

        var belowY = anchorBounds.Bottom + gap;
        var aboveY = anchorBounds.Top - gap - popupSize.Height;
        var belowFits = belowY + popupSize.Height
            <= clampBounds.Bottom - edgeMargin;
        var aboveFits = aboveY >= clampBounds.Top + edgeMargin;

        var placement = preferredPlacement switch
        {
            PopupAnchorPreferredPlacement.Below when belowFits
                => PopupAnchorPreferredPlacement.Below,
            PopupAnchorPreferredPlacement.Below
                => PopupAnchorPreferredPlacement.Above,
            PopupAnchorPreferredPlacement.Above when aboveFits
                => PopupAnchorPreferredPlacement.Above,
            PopupAnchorPreferredPlacement.Above
                => PopupAnchorPreferredPlacement.Below,
            _ => throw new ArgumentOutOfRangeException(
                nameof(preferredPlacement), preferredPlacement, null)
        };

        var y = placement == PopupAnchorPreferredPlacement.Below
            ? belowY
            : aboveY;
        return new PopupAnchorPlacement(new Point(x, y), placement);
    }

    private static void Validate(
        Rect anchorBounds,
        Size popupSize,
        Rect clampBounds,
        double gap,
        double edgeMargin)
    {
        if (anchorBounds.IsEmpty)
        {
            throw new ArgumentException("Anchor bounds cannot be empty.", nameof(anchorBounds));
        }

        if (clampBounds.IsEmpty)
        {
            throw new ArgumentException("Clamp bounds cannot be empty.", nameof(clampBounds));
        }

        if (!IsFiniteNonNegative(popupSize.Width)
            || !IsFiniteNonNegative(popupSize.Height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(popupSize), "Popup dimensions must be finite and non-negative.");
        }

        if (!IsFiniteNonNegative(gap))
        {
            throw new ArgumentOutOfRangeException(nameof(gap));
        }

        if (!IsFiniteNonNegative(edgeMargin))
        {
            throw new ArgumentOutOfRangeException(nameof(edgeMargin));
        }
    }

    private static bool IsFiniteNonNegative(double value)
        => double.IsFinite(value) && value >= 0;
}
