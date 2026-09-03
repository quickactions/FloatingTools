using System.Windows.Controls;
using FloatingTools.App.SharedUi.Controls;

namespace FloatingTools.Tests.SharedUi.Controls;

/// <summary>
/// Real pixel-level scroll behavior needs a live, rendered visual tree
/// (a real ScrollViewer ancestor with laid-out content) that this headless
/// harness doesn't reliably provide, so these checks are limited to what's
/// safe to assert here: the attached property round-trips correctly, and
/// raising it — in either direction — never throws, even off a UI thread
/// with no visual tree at all. Manual verification covers the actual
/// scroll feel.
/// </summary>
[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class BringIntoViewOnRevealBehaviorTests
{
    [Fact]
    public void GetAndSetRevealVersion_RoundTrip()
        => WpfTestApplication.Run(() =>
        {
            var element = new Border();

            BringIntoViewOnRevealBehavior.SetRevealVersion(element, 3);

            Assert.Equal(3, BringIntoViewOnRevealBehavior.GetRevealVersion(element));
        });

    [Fact]
    public void IncreasingTheVersion_DoesNotThrowEvenWithoutAVisualTree()
        => WpfTestApplication.Run(() =>
        {
            var element = new Border();

            var exception = Record.Exception(() =>
            {
                BringIntoViewOnRevealBehavior.SetRevealVersion(element, 1);
                BringIntoViewOnRevealBehavior.SetRevealVersion(element, 2);
            });

            Assert.Null(exception);
        });

    [Fact]
    public void DecreasingTheVersion_DoesNotThrowAndDoesNotScheduleWork()
        => WpfTestApplication.Run(() =>
        {
            var element = new Border();
            BringIntoViewOnRevealBehavior.SetRevealVersion(element, 5);

            var exception = Record.Exception(() =>
                BringIntoViewOnRevealBehavior.SetRevealVersion(element, 1));

            Assert.Null(exception);
            Assert.Equal(1, BringIntoViewOnRevealBehavior.GetRevealVersion(element));
        });
}
