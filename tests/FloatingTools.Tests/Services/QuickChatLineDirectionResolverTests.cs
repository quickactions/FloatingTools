using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.Tests.Services;

public sealed class QuickChatLineDirectionResolverTests
{
    [Theory]
    [InlineData("ב-Firefox, כברירת מחדל:")]
    [InlineData("לחצי Ctrl + Shift + S.")]
    [InlineData("TextBlock מציג את הטקסט")]
    [InlineData("צריך להשתמש ב-TextDirectionResolver בתוך ה-TextBlock.")]
    [InlineData("Windows מאפשר צילום מסך.")]
    [InlineData("### צילום מסך ב-Firefox")]
    [InlineData("1. לחצי על Ctrl + Shift + S")]
    [InlineData("Ctrl + Alt + T אינו קיצור ברירת המחדל של Firefox.")]
    [InlineData("Alt + Shift + T פותח כלי צילום מסך מהיר בעברית")]
    [InlineData("Ctrl + Alt + T מציג את כלי הצילום בתוך Firefox")]
    public void Resolve_HebrewFirstOrLatinFirstHebrewMajority_IsRightToLeft(string line)
    {
        var result = QuickChatLineDirectionResolver.Resolve(line);

        Assert.Equal(TextDirection.RightToLeft, result.Direction);
        Assert.Equal(TextDirectionAlignment.Right, result.Alignment);
    }

    [Theory]
    [InlineData("Take Screenshot")]
    [InlineData("Take Screenshot / צילום מסך")]
    [InlineData("The Hebrew word שלום means peace")]
    [InlineData("hello עולם")]
    public void Resolve_GenuineEnglishOrLatinFirstTie_IsLeftToRight(string line)
    {
        var result = QuickChatLineDirectionResolver.Resolve(line);

        Assert.Equal(TextDirection.LeftToRight, result.Direction);
        Assert.Equal(TextDirectionAlignment.Left, result.Alignment);
    }

    [Fact]
    public void Resolve_HebrewFirst_RemainsRightToLeftDespiteLatinTokenMajority()
    {
        var result = QuickChatLineDirectionResolver.Resolve(
            "שלום Windows TextBlock Firefox Screenshot");

        Assert.Equal(TextDirection.RightToLeft, result.Direction);
        Assert.Equal(TextDirectionAlignment.Right, result.Alignment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("---")]
    public void Resolve_EmptyOrNeutralOnlyLine_IsNeutral(string? line)
    {
        var result = QuickChatLineDirectionResolver.Resolve(line);

        Assert.Equal(TextDirection.Neutral, result.Direction);
        Assert.Equal(TextDirectionAlignment.Default, result.Alignment);
    }
}
