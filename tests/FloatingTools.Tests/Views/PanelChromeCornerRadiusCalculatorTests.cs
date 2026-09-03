using FloatingTools.App.Models;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

public sealed class PanelChromeCornerRadiusCalculatorTests
{
    [Fact]
    public void Calculate_DockRight_RoundsOnlyTheFreeLeftSide()
    {
        var radii = PanelChromeCornerRadiusCalculator.Calculate(DockSide.Right);

        Assert.Equal(new System.Windows.CornerRadius(12, 0, 0, 12), radii.Panel);
        Assert.Equal(new System.Windows.CornerRadius(12, 0, 0, 0), radii.Header);
        Assert.Equal(new System.Windows.CornerRadius(0, 0, 0, 12), radii.ActiveContent);
    }

    [Fact]
    public void Calculate_DockLeft_RoundsOnlyTheFreeRightSide()
    {
        var radii = PanelChromeCornerRadiusCalculator.Calculate(DockSide.Left);

        Assert.Equal(new System.Windows.CornerRadius(0, 12, 12, 0), radii.Panel);
        Assert.Equal(new System.Windows.CornerRadius(0, 12, 0, 0), radii.Header);
        Assert.Equal(new System.Windows.CornerRadius(0, 0, 12, 0), radii.ActiveContent);
    }
}
