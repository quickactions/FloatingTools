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

    [Theory]
    [InlineData(TopOpeningDirection.Right, 0, 12)]
    [InlineData(TopOpeningDirection.Left, 12, 0)]
    public void Calculate_DockTop_ConnectsTheUpperCornerBesideCube(
        TopOpeningDirection opening, double topLeft, double topRight)
    {
        var radii = PanelChromeCornerRadiusCalculator.Calculate(DockSide.Top, opening);

        Assert.Equal(topLeft, radii.Panel.TopLeft);
        Assert.Equal(topRight, radii.Panel.TopRight);
        Assert.Equal(12, radii.Panel.BottomLeft);
        Assert.Equal(12, radii.Panel.BottomRight);
        Assert.Equal(topLeft, radii.Header.TopLeft);
        Assert.Equal(topRight, radii.Header.TopRight);
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
