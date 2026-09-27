using System.Windows;
using FloatingTools.App.Models;

namespace FloatingTools.App.Views;

public readonly record struct PanelChromeCornerRadii(
    CornerRadius Panel,
    CornerRadius Header,
    CornerRadius ActiveContent);

public static class PanelChromeCornerRadiusCalculator
{
    private const double Radius = 12;

    public static PanelChromeCornerRadii Calculate(
        DockSide dockSide, TopOpeningDirection opening = TopOpeningDirection.Right) =>
        dockSide switch
        {
            DockSide.Right => new PanelChromeCornerRadii(
                new CornerRadius(Radius, 0, 0, Radius),
                new CornerRadius(Radius, 0, 0, 0),
                new CornerRadius(0, 0, 0, Radius)),
            DockSide.Left => new PanelChromeCornerRadii(
                new CornerRadius(0, Radius, Radius, 0),
                new CornerRadius(0, Radius, 0, 0),
                new CornerRadius(0, 0, Radius, 0)),
            DockSide.Top when opening == TopOpeningDirection.Right => new PanelChromeCornerRadii(
                new CornerRadius(0, Radius, Radius, Radius),
                new CornerRadius(0, Radius, 0, 0),
                new CornerRadius(0, 0, Radius, Radius)),
            DockSide.Top => new PanelChromeCornerRadii(
                new CornerRadius(Radius, 0, Radius, Radius),
                new CornerRadius(Radius, 0, 0, 0),
                new CornerRadius(0, 0, Radius, Radius)),
            _ => throw new ArgumentOutOfRangeException(nameof(dockSide))
        };
}
