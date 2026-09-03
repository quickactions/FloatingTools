using System.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.Models;

namespace FloatingTools.Tests.Services;

public sealed class ParagraphDirectionResolverTests
{
    [Theory]
    [InlineData("שלום עולם")]
    [InlineData("123 - שלום")]
    public void HebrewParagraph_IsRightToLeftAndRightAligned(string text)
    {
        Assert.Equal(FlowDirection.RightToLeft, ParagraphDirectionResolver.Resolve(text));
        Assert.Equal(TextAlignment.Right, ParagraphDirectionResolver.ResolveAlignment(text));
    }

    [Theory]
    [InlineData("Hello world")]
    [InlineData("123 - Hello")]
    public void EnglishParagraph_IsLeftToRightAndLeftAligned(string text)
    {
        Assert.Equal(FlowDirection.LeftToRight, ParagraphDirectionResolver.Resolve(text));
        Assert.Equal(TextAlignment.Left, ParagraphDirectionResolver.ResolveAlignment(text));
    }

    [Fact]
    public void MixedNote_ResolvesEachParagraphIndependently()
    {
        var paragraphs = new[] { "English paragraph", "פסקה בעברית" };

        Assert.Collection(
            paragraphs.Select(ParagraphDirectionResolver.Resolve),
            direction => Assert.Equal(FlowDirection.LeftToRight, direction),
            direction => Assert.Equal(FlowDirection.RightToLeft, direction));
    }

    [Theory]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    public void Direction_IsIndependentOfDockSide(DockSide _)
    {
        Assert.Equal(
            FlowDirection.RightToLeft,
            ParagraphDirectionResolver.Resolve("... 123 שלום"));
        Assert.Equal(
            FlowDirection.LeftToRight,
            ParagraphDirectionResolver.Resolve("... 123 Hello"));
    }
}
