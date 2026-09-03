using FloatingTools.App.Views;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.Views;

public sealed class QuickChatImageDropPolicyTests
{
    [Theory]
    [InlineData("image.png")]
    [InlineData("image.PNG")]
    [InlineData("image.jpg")]
    [InlineData("image.jpeg")]
    public void PngAndJpegAreAccepted(string path) =>
        Assert.True(QuickChatImageDropPolicy.IsSupported(path));

    [Theory]
    [InlineData("image.webp")]
    [InlineData("image.gif")]
    [InlineData("document.txt")]
    [InlineData("")]
    public void UnsupportedFilesAreRejected(string path) =>
        Assert.False(QuickChatImageDropPolicy.IsSupported(path));

    [Fact]
    public void MultipleFilesPreserveOrderFilterUnsupportedAndRespectCapacity()
    {
        var selected = QuickChatImageDropPolicy.SelectSupported(
            ["one.png", "skip.txt", "two.JPG", "three.jpeg"],
            availableCapacity: 2);

        Assert.Equal(["one.png", "two.JPG"], selected);
        Assert.Empty(QuickChatImageDropPolicy.SelectSupported(["one.png"], 0));
    }

    [Fact]
    public void FourthDroppedImageIsRejectedWhenThreeSlotsAreAlreadyUsed()
    {
        var capacity = QuickChatViewModel.MaximumPendingAttachmentCount - 3;

        Assert.Equal(3, QuickChatViewModel.MaximumPendingAttachmentCount);
        Assert.Empty(QuickChatImageDropPolicy.SelectSupported(["fourth.png"], capacity));
    }
}
