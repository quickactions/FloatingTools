using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class ApplicationVisibilitySessionTests
{
    [Fact]
    public void Hide_FromShown_SucceedsAndRemembersPanelWasVisible()
    {
        var session = new ApplicationVisibilitySession();

        var result = session.Hide(isPanelCurrentlyVisible: true);

        Assert.True(result);
        Assert.True(session.IsHidden);
        Assert.True(session.PanelWasVisibleBeforeHide);
    }

    [Fact]
    public void Hide_RemembersWhenPanelWasNotVisible()
    {
        var session = new ApplicationVisibilitySession();

        session.Hide(isPanelCurrentlyVisible: false);

        Assert.False(session.PanelWasVisibleBeforeHide);
    }

    [Fact]
    public void Hide_WhileAlreadyHidden_IsANoOpAndReturnsFalse()
    {
        var session = new ApplicationVisibilitySession();
        session.Hide(isPanelCurrentlyVisible: true);

        var secondHide = session.Hide(isPanelCurrentlyVisible: false);

        Assert.False(secondHide);
        Assert.True(session.IsHidden);
        Assert.True(session.PanelWasVisibleBeforeHide);
    }

    [Fact]
    public void Show_WhileHidden_SucceedsAndClearsIsHidden()
    {
        var session = new ApplicationVisibilitySession();
        session.Hide(isPanelCurrentlyVisible: true);

        var result = session.Show();

        Assert.True(result);
        Assert.False(session.IsHidden);
    }

    [Fact]
    public void Show_WhileNotHidden_IsANoOpAndReturnsFalse()
    {
        var session = new ApplicationVisibilitySession();

        var result = session.Show();

        Assert.False(result);
        Assert.False(session.IsHidden);
    }

    [Fact]
    public void RepeatedHideShowCycles_AreSafeAndTrackStateCorrectlyEachTime()
    {
        var session = new ApplicationVisibilitySession();

        Assert.True(session.Hide(isPanelCurrentlyVisible: true));
        Assert.True(session.Show());
        Assert.True(session.Hide(isPanelCurrentlyVisible: false));
        Assert.False(session.PanelWasVisibleBeforeHide);
        Assert.True(session.Show());
        Assert.False(session.IsHidden);
    }
}
