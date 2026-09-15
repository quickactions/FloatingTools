using System.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class QuickChatMessageViewModelTests
{
    [Theory]
    [InlineData("שלום", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("hello", FlowDirection.LeftToRight, TextAlignment.Left)]
    [InlineData("123... שלום", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("123... hello", FlowDirection.LeftToRight, TextAlignment.Left)]
    [InlineData("שלום world", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("hello עולם", FlowDirection.LeftToRight, TextAlignment.Left)]
    [InlineData("123 שלום 456", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("?! שלום!", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("(שלום) world", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("שלום https://example.com עולם", FlowDirection.RightToLeft, TextAlignment.Left)]
    public void ParagraphProjection_UsesFirstStrongDirectionPerParagraph(
        string text,
        FlowDirection expectedFlow,
        TextAlignment expectedAlignment)
    {
        var presentation = new QuickChatMessageViewModel(Message(text));

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal(text, paragraph.Text);
        Assert.Equal(expectedFlow, paragraph.FlowDirection);
        Assert.Equal(expectedAlignment, paragraph.TextAlignment);
    }

    [Fact]
    public void ParagraphProjection_ResolvesLogicalParagraphsAndPreservesBlankLines()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("hello\r\n\r\nשלום\n123 !!!"));

        Assert.Equal(["hello", "", "שלום\n123 !!!"],
            presentation.Paragraphs.Select(item => item.Text));
        Assert.Equal(FlowDirection.LeftToRight, presentation.Paragraphs[0].FlowDirection);
        Assert.Equal(FlowDirection.LeftToRight, presentation.Paragraphs[1].FlowDirection);
        Assert.Equal(FlowDirection.RightToLeft, presentation.Paragraphs[2].FlowDirection);
    }

    [Theory]
    [InlineData("שלום עולם זהו משפט עברי ארוך שאמור להישאר פסקה אחת ולהיעטף באופן טבעי בתוך רוחב התצוגה", FlowDirection.RightToLeft, TextAlignment.Left)]
    [InlineData("This is one long English paragraph that should remain one presentation element and wrap naturally within the available width.", FlowDirection.LeftToRight, TextAlignment.Left)]
    public void LongLogicalParagraph_RemainsOneWrappingPresentationElement(
        string text,
        FlowDirection expectedFlow,
        TextAlignment expectedAlignment)
    {
        var presentation = new QuickChatMessageViewModel(Message(text));

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal(text, paragraph.Text);
        Assert.Equal(expectedFlow, paragraph.FlowDirection);
        Assert.Equal(expectedAlignment, paragraph.TextAlignment);
    }

    [Fact]
    public void StreamingUpdate_ReprojectsExistingMessagePresentation()
    {
        var message = Message("hello");
        var presentation = new QuickChatMessageViewModel(message);

        message.Text = "hello\ncontinued hard line";
        presentation.UpdateFrom(message);

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal("hello\ncontinued hard line", paragraph.Text);
        Assert.Equal(FlowDirection.LeftToRight, paragraph.FlowDirection);
        Assert.Equal("hello\ncontinued hard line", presentation.Text);
    }

    [Fact]
    public void StreamingExtension_KeepsOneLogicalParagraphAndReusesMessagePresentation()
    {
        var message = Message("שלום");
        var presentation = new QuickChatMessageViewModel(message);

        message.Text += " עולם with embedded English that keeps extending";
        presentation.UpdateFrom(message);

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal(message.Text, paragraph.Text);
        Assert.Equal(FlowDirection.RightToLeft, paragraph.FlowDirection);
        Assert.Equal(TextAlignment.Left, paragraph.TextAlignment);
    }

    [Fact]
    public void BlankLineSeparatesParagraphDirectionsButSingleHardBreakDoesNot()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("שלום\nשורת המשך\n\nEnglish paragraph\ncontinued line"));

        Assert.Equal(["שלום\nשורת המשך", "", "English paragraph\ncontinued line"],
            presentation.Paragraphs.Select(item => item.Text));
        Assert.Equal(FlowDirection.RightToLeft, presentation.Paragraphs[0].FlowDirection);
        Assert.Equal(FlowDirection.LeftToRight, presentation.Paragraphs[2].FlowDirection);
    }

    [Fact]
    public void MinimalPresentationNormalizationRemovesEmphasisAndInlineCodeMarkersOnly()
    {
        const string raw = "**שלום** עם `inline` וגם *הדגשה*";
        var presentation = new QuickChatMessageViewModel(Message(raw));

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal("שלום עם inline וגם הדגשה", paragraph.Text);
        Assert.Equal(FlowDirection.RightToLeft, paragraph.FlowDirection);
        Assert.Equal(raw, presentation.Text);
    }

    [Fact]
    public void SimpleHebrewBulletListUsesCleanBulletsInOneContinuousTextFlow()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("- פריט ראשון\n* פריט שני"));

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal("• פריט ראשון\n• פריט שני", paragraph.Text);
        Assert.Equal(FlowDirection.RightToLeft, paragraph.FlowDirection);
    }

    [Fact]
    public void MixedDirectionHardLines_SplitIntoSeparateParagraphs()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("שורה עברית\nEnglish line\nשורה עברית נוספת"));

        Assert.Equal(
            ["שורה עברית", "English line", "שורה עברית נוספת"],
            presentation.Paragraphs.Select(item => item.Text));
        Assert.Equal(
            [FlowDirection.RightToLeft, FlowDirection.LeftToRight, FlowDirection.RightToLeft],
            presentation.Paragraphs.Select(item => item.FlowDirection));
    }

    [Theory]
    [InlineData("שורה עברית\n123\nעוד עברית", "שורה עברית\n123\nעוד עברית")]
    [InlineData("1.\nשורה עברית", "1.\nשורה עברית")]
    public void NeutralHardLine_JoinsSurroundingDirectionRun(string text, string expected)
    {
        var presentation = new QuickChatMessageViewModel(Message(text));

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal(expected, paragraph.Text);
        Assert.Equal(FlowDirection.RightToLeft, paragraph.FlowDirection);
    }

    [Fact]
    public void MixedBulletList_SplitsAtDirectionChangeOnly()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("- פריט עברי\n- English item\n- פריט עברי נוסף"));

        Assert.Equal(
            ["• פריט עברי", "• English item", "• פריט עברי נוסף"],
            presentation.Paragraphs.Select(item => item.Text));
        Assert.Equal(
            [FlowDirection.RightToLeft, FlowDirection.LeftToRight, FlowDirection.RightToLeft],
            presentation.Paragraphs.Select(item => item.FlowDirection));
    }

    [Fact]
    public void StreamingNeutralLineBecomesEnglishAfterHebrew()
    {
        var message = Message("שורה עברית\n------");
        var presentation = new QuickChatMessageViewModel(message);

        var initial = Assert.Single(presentation.Paragraphs);
        Assert.Equal("שורה עברית\n------", initial.Text);
        Assert.Equal(FlowDirection.RightToLeft, initial.FlowDirection);

        message.Text = "שורה עברית\n------ English";
        presentation.UpdateFrom(message);

        Assert.Equal(["שורה עברית", "------ English"],
            presentation.Paragraphs.Select(item => item.Text));
        Assert.Equal(FlowDirection.LeftToRight, presentation.Paragraphs[1].FlowDirection);

        message.Text += " עברית";
        presentation.UpdateFrom(message);

        Assert.Equal(["שורה עברית", "------ English עברית"],
            presentation.Paragraphs.Select(item => item.Text));
        Assert.Equal(FlowDirection.LeftToRight, presentation.Paragraphs[1].FlowDirection);
    }

    [Fact]
    public void ScreenshotStyleFirefoxResponse_UsesLineDirectionsAndPreservesSpacing()
    {
        var presentation = new QuickChatMessageViewModel(Message(
            "### צילום מסך ב-Firefox\n\n" +
            "- Alt + Shift + T פותח כלי צילום מסך מהיר בעברית\n" +
            "- Ctrl + Alt + T אינו קיצור ברירת המחדל של Firefox.\n\n" +
            "1. לחצי על Ctrl + Shift + S"));

        Assert.Equal(
            [
                "### צילום מסך ב-Firefox",
                "",
                "• Alt + Shift + T פותח כלי צילום מסך מהיר בעברית\n" +
                "• Ctrl + Alt + T אינו קיצור ברירת המחדל של Firefox.",
                "",
                "1. לחצי על Ctrl + Shift + S"
            ],
            presentation.Paragraphs.Select(item => item.Text));
        Assert.All(
            presentation.Paragraphs.Where(item => item.Text.Length > 0),
            item => Assert.Equal(FlowDirection.RightToLeft, item.FlowDirection));
    }

    [Fact]
    public void LatinFirstHebrewBullet_IsRightToLeft()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("- Windows מאפשר צילום מסך מהיר"));

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal("• Windows מאפשר צילום מסך מהיר", paragraph.Text);
        Assert.Equal(FlowDirection.RightToLeft, paragraph.FlowDirection);
    }

    [Fact]
    public void EnglishLineInsideHebrewList_StillSplits()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("שורה עברית\nTake Screenshot\nשורה עברית נוספת"));

        Assert.Equal(
            [FlowDirection.RightToLeft, FlowDirection.LeftToRight, FlowDirection.RightToLeft],
            presentation.Paragraphs.Select(item => item.FlowDirection));
    }

    [Fact]
    public void RunResolutionIsCarriedNotRecomputed()
    {
        var presentation = new QuickChatMessageViewModel(
            Message("Windows מאפשר צילום מסך\nלחצי Ctrl + Shift + S"));

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal("Windows מאפשר צילום מסך\nלחצי Ctrl + Shift + S", paragraph.Text);
        Assert.Equal(FlowDirection.RightToLeft, paragraph.FlowDirection);
    }

    [Fact]
    public void StreamingLatinFirstLine_FlipsToHebrewAndRejoinsRun()
    {
        var message = Message("שורה עברית\nCtrl + Alt + T");
        var presentation = new QuickChatMessageViewModel(message);

        Assert.Equal(2, presentation.Paragraphs.Count);
        Assert.Equal(FlowDirection.RightToLeft, presentation.Paragraphs[0].FlowDirection);
        Assert.Equal(FlowDirection.LeftToRight, presentation.Paragraphs[1].FlowDirection);

        message.Text += " אינו קיצור ברירת המחדל";
        presentation.UpdateFrom(message);

        var paragraph = Assert.Single(presentation.Paragraphs);
        Assert.Equal(message.Text, paragraph.Text);
        Assert.Equal(FlowDirection.RightToLeft, paragraph.FlowDirection);
    }

    private static QuickChatMessage Message(string text) => new()
    {
        Id = Guid.NewGuid(),
        Role = QuickChatMessageRole.Assistant,
        Text = text,
        Status = QuickChatMessageStatus.InProgress,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
