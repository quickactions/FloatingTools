using System.Windows;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.Tests.SharedUi.Direction;

public sealed class TextDirectionWpfExtensionsTests
{
    [Theory]
    [InlineData(TextDirection.RightToLeft, FlowDirection.RightToLeft)]
    [InlineData(TextDirection.LeftToRight, FlowDirection.LeftToRight)]
    [InlineData(TextDirection.Neutral, FlowDirection.LeftToRight)]
    public void ToFlowDirectionMapsSemanticDirectionWithNeutralLtrFallback(
        TextDirection direction,
        FlowDirection expected)
    {
        var resolution = new TextDirectionResolution(
            direction,
            TextDirectionAlignment.Default);

        Assert.Equal(expected, resolution.ToFlowDirection());
    }

    [Theory]
    [InlineData(TextDirection.RightToLeft, TextDirectionAlignment.Right, TextAlignment.Left)]
    [InlineData(TextDirection.RightToLeft, TextDirectionAlignment.Left, TextAlignment.Right)]
    [InlineData(TextDirection.LeftToRight, TextDirectionAlignment.Left, TextAlignment.Left)]
    [InlineData(TextDirection.LeftToRight, TextDirectionAlignment.Right, TextAlignment.Right)]
    [InlineData(TextDirection.Neutral, TextDirectionAlignment.Default, TextAlignment.Left)]
    public void ToPhysicalTextAlignmentAccountsForTheMirroredRtlCoordinateSystem(
        TextDirection direction,
        TextDirectionAlignment alignment,
        TextAlignment expected)
    {
        var resolution = new TextDirectionResolution(direction, alignment);

        Assert.Equal(expected, resolution.ToPhysicalTextAlignment());
    }

    [Theory]
    [InlineData("שלום", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("hello", FlowDirection.LeftToRight, TextAlignment.Left)]
    [InlineData("123 !!!", FlowDirection.LeftToRight, TextAlignment.Left)]
    public void ResolverOutputMapsToPhysicalLanguageSide(
        string text,
        FlowDirection expectedFlow,
        TextAlignment expectedAlignment)
    {
        var resolution = TextDirectionResolver.Resolve(text);

        Assert.Equal(expectedFlow, resolution.ToFlowDirection());
        Assert.Equal(expectedAlignment, resolution.ToPhysicalTextAlignment());
    }
}
