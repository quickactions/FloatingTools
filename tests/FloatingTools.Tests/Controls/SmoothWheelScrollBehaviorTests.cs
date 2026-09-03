using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class SmoothWheelScrollBehaviorTests
{
    [Theory]
    [InlineData(15, 2.5)]
    [InlineData(-15, -2.5)]
    [InlineData(120, 20)]
    [InlineData(-120, -20)]
    public void NormalizeDelta_ProducesConservativePixelMovement(
        int delta,
        double expected)
    {
        Assert.Equal(expected, SmoothWheelScrollBehavior.NormalizeDelta(delta));
    }

    [Theory]
    [InlineData(240, 40)]
    [InlineData(960, 40)]
    [InlineData(-960, -40)]
    public void NormalizeDelta_ClampsOneEventBelowPageSizedMovement(
        int delta,
        double expected)
    {
        Assert.Equal(expected, SmoothWheelScrollBehavior.NormalizeDelta(delta));
        Assert.True(Math.Abs(expected) < 100);
    }

    [Theory]
    [InlineData(5, 20, 100, 0)]
    [InlineData(95, -20, 100, 100)]
    [InlineData(50, 12.5, 100, 37.5)]
    public void CalculateTargetOffset_ClampsAndPreservesFractionalMovement(
        double currentTarget,
        double movement,
        double maximum,
        double expected)
    {
        Assert.Equal(expected,
            SmoothWheelScrollBehavior.CalculateTargetOffset(
                currentTarget, movement, maximum));
    }

    [Fact]
    public void InterpolateOffset_MovesTowardTargetWithoutOvershoot()
    {
        var next = SmoothWheelScrollBehavior.InterpolateOffset(10, 30, 16);

        Assert.InRange(next, 10.01, 29.99);
        Assert.Equal(30,
            SmoothWheelScrollBehavior.InterpolateOffset(29.9, 30, 16));
    }
}
