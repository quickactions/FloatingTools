using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class ResponsiveImageSizeCalculatorTests
{
    [Fact]
    public void NarrowPanel_ClampsEffectiveWidthAndScalesHeight()
    {
        var result = ResponsiveImageSizeCalculator.Calculate(600, 332, 1.5);

        Assert.Equal(300, result.Width);
        Assert.Equal(200, result.Height);
    }

    [Fact]
    public void WiderPanel_RestoresRequestedSavedWidth()
    {
        var standard = ResponsiveImageSizeCalculator.Calculate(600, 332, 1.5);
        var large = ResponsiveImageSizeCalculator.Calculate(600, 700, 1.5);

        Assert.Equal(300, standard.Width);
        Assert.Equal(600, large.Width);
        Assert.Equal(400, large.Height);
    }

    [Fact]
    public void InvalidInputs_ProduceFinitePositiveSize()
    {
        var result = ResponsiveImageSizeCalculator.Calculate(
            double.NaN,
            double.PositiveInfinity,
            0);

        Assert.True(double.IsFinite(result.Width));
        Assert.True(double.IsFinite(result.Height));
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }
}
