using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.Tests.SharedUi.Direction;

public sealed class TextDirectionResolverTests
{
    [Theory]
    [InlineData("שלום", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("hello", TextDirection.LeftToRight, TextDirectionAlignment.Left)]
    [InlineData("   שלום", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("   hello", TextDirection.LeftToRight, TextDirectionAlignment.Left)]
    [InlineData("123 שלום", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("123 hello", TextDirection.LeftToRight, TextDirectionAlignment.Left)]
    [InlineData("123, שלום world!", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("... 123 Hello", TextDirection.LeftToRight, TextDirectionAlignment.Left)]
    [InlineData("... שלום", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("... hello", TextDirection.LeftToRight, TextDirectionAlignment.Left)]
    [InlineData("שלום world", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("hello שלום", TextDirection.LeftToRight, TextDirectionAlignment.Left)]
    [InlineData("\"שלום\"", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("״שלום״", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("\nשלום", TextDirection.RightToLeft, TextDirectionAlignment.Right)]
    [InlineData("\thello", TextDirection.LeftToRight, TextDirectionAlignment.Left)]
    public void Resolve_UsesTheFirstStrongHebrewOrEnglishCharacter(
        string text,
        TextDirection expectedDirection,
        TextDirectionAlignment expectedAlignment)
    {
        var result = TextDirectionResolver.Resolve(text);

        Assert.Equal(expectedDirection, result.Direction);
        Assert.Equal(expectedAlignment, result.Alignment);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("!?.,;:—")]
    [InlineData("  \r\n\t  ")]
    [InlineData("")]
    [InlineData("׳״־")]
    public void Resolve_NeutralText_UsesTheDefaultAlignment(string text)
    {
        var result = TextDirectionResolver.Resolve(text);

        Assert.Equal(TextDirection.Neutral, result.Direction);
        Assert.Equal(TextDirectionAlignment.Default, result.Alignment);
    }

    [Fact]
    public void Resolve_Null_UsesTheNeutralDefault()
    {
        var result = TextDirectionResolver.Resolve(null);

        Assert.Equal(TextDirection.Neutral, result.Direction);
        Assert.Equal(TextDirectionAlignment.Default, result.Alignment);
    }
}
