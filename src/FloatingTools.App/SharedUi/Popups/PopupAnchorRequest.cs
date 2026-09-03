using System.Windows;
using System.Windows.Controls;

namespace FloatingTools.App.SharedUi.Popups;

public enum PopupAnchorPreferredPlacement
{
    Below,
    Above
}

/// <summary>
/// Describes one feature-agnostic contextual popup. Consumers provide WPF elements;
/// coordinate translation and placement remain the responsibility of the popup host.
/// </summary>
public sealed class PopupAnchorRequest
{
    private readonly Func<FrameworkElement> _contentFactory;

    public PopupAnchorRequest(
        FrameworkElement target,
        FrameworkElement content,
        FrameworkElement clampBoundsElement)
        : this(target, () => content, clampBoundsElement)
    {
    }

    public PopupAnchorRequest(
        FrameworkElement target,
        Func<FrameworkElement> contentFactory,
        FrameworkElement clampBoundsElement)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _contentFactory = contentFactory ?? throw new ArgumentNullException(nameof(contentFactory));
        ClampBoundsElement = clampBoundsElement
            ?? throw new ArgumentNullException(nameof(clampBoundsElement));
    }

    public FrameworkElement Target { get; }

    public FrameworkElement ClampBoundsElement { get; }

    public PopupAnchorPreferredPlacement PreferredPlacement { get; init; }
        = PopupAnchorPreferredPlacement.Below;

    public double Gap { get; init; } = 4;

    public double EdgeMargin { get; init; } = 5;

    public bool CloseOnExternalClick { get; init; } = true;

    public ScrollViewer? CloseOnScrollOf { get; init; }

    public bool SingleInstance { get; init; } = true;

    internal FrameworkElement CreateContent()
    {
        var content = _contentFactory();
        return content ?? throw new InvalidOperationException(
            "The popup content factory returned null.");
    }
}
