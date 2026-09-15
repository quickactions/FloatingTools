using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class CaretScrollMarginBehaviorTests
{
    [Fact]
    public void CalculateOffset_ReturnsNullWhenRectIsAlreadyComfortable()
    {
        Assert.Null(CaretScrollMarginBehavior.CalculateOffset(
            130, 148, 100, 200, 500, 8, 24));
    }

    [Fact]
    public void CalculateOffset_ReturnsExactDownwardOffset()
    {
        Assert.Equal(142, CaretScrollMarginBehavior.CalculateOffset(
            300, 318, 100, 200, 500, 8, 24));
    }

    [Fact]
    public void CalculateOffset_ReturnsExactUpwardOffset()
    {
        Assert.Equal(92, CaretScrollMarginBehavior.CalculateOffset(
            100, 118, 120, 200, 500, 8, 24));
    }

    [Fact]
    public void CalculateOffset_ClampsAtZero()
    {
        Assert.Equal(0, CaretScrollMarginBehavior.CalculateOffset(
            2, 20, 10, 100, 500, 8, 24));
    }

    [Fact]
    public void CalculateOffset_ClampsAtScrollableHeight()
    {
        Assert.Equal(410, CaretScrollMarginBehavior.CalculateOffset(
            490, 510, 400, 100, 410, 8, 24));
    }

    [Fact]
    public void CalculateOffset_DeclinesRectTooLargeForComfortableViewport()
    {
        Assert.Null(CaretScrollMarginBehavior.CalculateOffset(
            100, 170, 100, 100, 500, 8, 24));
    }
}
