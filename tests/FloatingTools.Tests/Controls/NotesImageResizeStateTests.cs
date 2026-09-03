using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class NotesImageResizeStateTests
{
    [Fact]
    public void Update_ClampsToMaximumCapturedAtStart()
    {
        var target = new NotesImageResizeState(200, 60, 320);
        double preview = 0;

        var changed = target.TryUpdate(10_000, 0, 2, value => preview = value);

        Assert.True(changed);
        Assert.Equal(320, preview);
        Assert.Equal(320, target.ProposedWidth);
        Assert.Equal(320, target.MaximumWidth);
    }

    [Fact]
    public void RepeatedMovementBeyondMaximum_DoesNotReapplyPreview()
    {
        var target = new NotesImageResizeState(200, 60, 320);
        var previews = 0;

        target.TryUpdate(10_000, 0, 2, _ => previews++);
        var changed = target.TryUpdate(20_000, 0, 2, _ => previews++);

        Assert.False(changed);
        Assert.Equal(1, previews);
    }

    [Fact]
    public void PreviewCallback_CannotReenterUpdate()
    {
        var target = new NotesImageResizeState(200, 60, 320);
        var recursiveChanged = true;

        target.TryUpdate(20, 0, 2, _ =>
            recursiveChanged = target.TryUpdate(30, 0, 2, _ => { }));

        Assert.False(recursiveChanged);
        Assert.Equal(240, target.ProposedWidth);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidPointerValues_NeverProduceInvalidWidth(double invalidValue)
    {
        var target = new NotesImageResizeState(200, 60, 320);

        target.TryUpdate(invalidValue, invalidValue, double.NaN, _ => { });

        Assert.True(double.IsFinite(target.ProposedWidth));
        Assert.InRange(target.ProposedWidth, 60, 320);
    }

    [Fact]
    public void Complete_ReturnsFinalWidthExactlyOnce()
    {
        var target = new NotesImageResizeState(200, 60, 320);
        target.TryUpdate(20, 0, 2, _ => { });

        Assert.True(target.TryComplete(out var first));
        Assert.False(target.TryComplete(out var second));
        Assert.Equal(240, first);
        Assert.Equal(first, second);
    }
}
