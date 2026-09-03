using System.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class ScreenTextCaptureLifecycleTests
{
    [Fact]
    public async Task SelectionSuccess_ClosesOverlayAndReturnsSelection()
    {
        var selection = new PixelRect(10, 20, 30, 40);
        var overlay = new StubOverlay(() => Task.FromResult<PixelRect?>(selection));

        var result = await ScreenTextCaptureService.SelectWithGuaranteedCleanupAsync(
            overlay,
            owner: null);

        Assert.Equal(selection, result);
        Assert.Equal(1, overlay.CloseCount);
    }

    [Fact]
    public async Task SelectionCancellation_ClosesOverlay()
    {
        var overlay = new StubOverlay(() => Task.FromResult<PixelRect?>(null));

        var result = await ScreenTextCaptureService.SelectWithGuaranteedCleanupAsync(
            overlay,
            owner: null);

        Assert.Null(result);
        Assert.Equal(1, overlay.CloseCount);
    }

    [Fact]
    public async Task SelectionFailure_ClosesOverlayBeforePropagating()
    {
        var overlay = new StubOverlay(() =>
            Task.FromException<PixelRect?>(new InvalidOperationException("Selection failed.")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ScreenTextCaptureService.SelectWithGuaranteedCleanupAsync(
                overlay,
                owner: null));

        Assert.Equal(1, overlay.CloseCount);
    }

    [Fact]
    public void Selection_AssignsOwnerBeforeShowingOverlay()
    {
        WpfTestApplication.Run(() =>
        {
            var owner = new Window();
            var overlay = new StubOverlay(() => Task.FromResult<PixelRect?>(null));

            ScreenTextCaptureService.SelectWithGuaranteedCleanupAsync(overlay, owner)
                .GetAwaiter()
                .GetResult();

            Assert.Same(owner, overlay.OwnerAtSelection);
            Assert.Equal(1, overlay.CloseCount);
            owner.Close();
        });
    }

    private sealed class StubOverlay(Func<Task<PixelRect?>> selectAsync)
        : IScreenCaptureOverlay
    {
        public Window? Owner { get; set; }

        public Window? OwnerAtSelection { get; private set; }

        public int CloseCount { get; private set; }

        public Task<PixelRect?> SelectAsync()
        {
            OwnerAtSelection = Owner;
            return selectAsync();
        }

        public void CloseOverlay() => CloseCount++;
    }
}
