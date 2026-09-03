using System.Windows;
using FloatingTools.App.SharedUi.Popups;

namespace FloatingTools.Tests.SharedUi.Popups;

public sealed class PopupAnchorPlacementCalculatorTests
{
    [Fact]
    public void Calculate_CentersBelowAnchorWhenThereIsRoom()
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(100, 30, 80, 20),
            new Size(170, 28),
            new Rect(0, 0, 320, 200),
            gap: 6,
            edgeMargin: 4);

        Assert.Equal(new Point(55, 56), result.Position);
        Assert.Equal(PopupAnchorPreferredPlacement.Below, result.Placement);
    }

    [Fact]
    public void Calculate_BelowUsesRequestedGap()
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(80, 40, 40, 20),
            new Size(120, 33),
            new Rect(0, 0, 300, 220),
            gap: 9);

        Assert.Equal(69, result.Position.Y);
    }

    [Fact]
    public void Calculate_FlipsAboveOnlyOnRealBottomOverflow()
    {
        var exactFit = PopupAnchorPlacementCalculator.Calculate(
            new Rect(80, 148, 40, 20),
            new Size(100, 43),
            new Rect(0, 0, 260, 220));
        var overflow = PopupAnchorPlacementCalculator.Calculate(
            new Rect(80, 149, 40, 20),
            new Size(100, 43),
            new Rect(0, 0, 260, 220));

        Assert.Equal(PopupAnchorPreferredPlacement.Below, exactFit.Placement);
        Assert.Equal(PopupAnchorPreferredPlacement.Above, overflow.Placement);
        Assert.Equal(102, overflow.Position.Y);
    }

    [Fact]
    public void Calculate_FlipsAboveUsingTheLegacyAnchoredGapAndMargin()
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(100, 180, 80, 15),
            new Size(170, 28),
            new Rect(0, 0, 320, 200),
            gap: 6,
            edgeMargin: 4);

        Assert.Equal(new Point(55, 146), result.Position);
        Assert.Equal(PopupAnchorPreferredPlacement.Above, result.Placement);
    }

    [Theory]
    [InlineData(-20, 5)]
    [InlineData(260, 215)]
    public void Calculate_ClampsHorizontally(
        double anchorLeft,
        double expectedLeft)
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(anchorLeft, 40, 100, 20),
            new Size(80, 60),
            new Rect(0, 0, 300, 220));

        Assert.Equal(expectedLeft, result.Position.X, 3);
    }

    [Theory]
    [InlineData(-50, 4)]
    [InlineData(310, 146)]
    public void Calculate_ClampsWithTheLegacyAnchoredMargin(
        double anchorLeft,
        double expectedLeft)
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(anchorLeft, 30, 20, 20),
            new Size(170, 28),
            new Rect(0, 0, 320, 200),
            gap: 6,
            edgeMargin: 4);

        Assert.Equal(expectedLeft, result.Position.X, 3);
        Assert.Equal(PopupAnchorPreferredPlacement.Below, result.Placement);
    }

    [Fact]
    public void Calculate_WidePopupThatFitsMarginsRemainsInsideBounds()
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(140, 40, 20, 20),
            new Size(290, 40),
            new Rect(0, 0, 300, 220));

        Assert.Equal(5, result.Position.X);
        Assert.Equal(295, result.Position.X + 290);
    }

    [Fact]
    public void Calculate_AnchorNearTopStillUsesBelow()
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(100, 1, 40, 20),
            new Size(100, 70),
            new Rect(0, 0, 260, 220));

        Assert.Equal(PopupAnchorPreferredPlacement.Below, result.Placement);
        Assert.Equal(25, result.Position.Y);
    }

    [Fact]
    public void Calculate_PreferredAboveFallsBackBelowNearTop()
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(100, 1, 40, 20),
            new Size(100, 70),
            new Rect(0, 0, 260, 220),
            PopupAnchorPreferredPlacement.Above);

        Assert.Equal(PopupAnchorPreferredPlacement.Below, result.Placement);
    }

    [Fact]
    public void Calculate_DifferentPopupSizesProduceNewCenteredPositions()
    {
        var anchor = new Rect(80, 40, 40, 20);
        var bounds = new Rect(0, 0, 300, 220);

        var compact = PopupAnchorPlacementCalculator.Calculate(
            anchor, new Size(120, 33), bounds);
        var edit = PopupAnchorPlacementCalculator.Calculate(
            anchor, new Size(188, 62), bounds);

        Assert.Equal(40, compact.Position.X);
        Assert.Equal(6, edit.Position.X);
        Assert.Equal(64, compact.Position.Y);
        Assert.Equal(64, edit.Position.Y);
    }

    [Fact]
    public void Calculate_UsesArbitraryBoundsWithoutPanelPresetAssumptions()
    {
        var bounds = new Rect(20, 10, 713, 419);
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(580, 90, 37, 18),
            new Size(211, 47),
            bounds,
            gap: 7,
            edgeMargin: 9);

        Assert.Equal(493, result.Position.X, 3);
        Assert.Equal(115, result.Position.Y, 3);
    }

    [Theory]
    [InlineData(320, 55)]
    [InlineData(713, 55)]
    public void Calculate_UsesTheSameCenteringRuleAtStandardAndLargeArbitraryWidths(
        double clampWidth,
        double expectedPopupLeft)
    {
        var result = PopupAnchorPlacementCalculator.Calculate(
            new Rect(100, 30, 80, 20),
            new Size(170, 28),
            new Rect(0, 0, clampWidth, 200),
            gap: 6,
            edgeMargin: 4);

        Assert.Equal(expectedPopupLeft, result.Position.X, 3);
        Assert.Equal(PopupAnchorPreferredPlacement.Below, result.Placement);
    }
}
