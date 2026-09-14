using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class CalendarEventsServicesTests
{
    private readonly CalendarEventsQueryService _query = new();
    private readonly CalendarEventPreviewGenerator _preview = new();

    [Fact]
    public void Query_IncludesBothRangeBoundariesAndExcludesOutsideDates()
    {
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 30);
        var entries = new[]
        {
            Entry(from.AddDays(-1), "before"), Entry(from, "first"),
            Entry(to, "last"), Entry(to.AddDays(1), "after")
        };

        Assert.Equal(["first", "last"],
            _query.Query(entries, from, to).Select(entry => entry.Text));
    }

    [Fact]
    public void Query_SortsDatesInEitherDirectionWithoutReversingSameDateCreationOrder()
    {
        var early = new DateOnly(2026, 9, 8);
        var late = early.AddDays(1);
        var entries = new[]
        {
            Entry(late, "late first"), Entry(early, "early first"),
            Entry(late, "late second"), Entry(early, "early second")
        };

        Assert.Equal(["early first", "early second", "late first", "late second"],
            _query.Query(entries, early, late).Select(entry => entry.Text));
        Assert.Equal(["late first", "late second", "early first", "early second"],
            _query.Query(entries, early, late, descending: true).Select(entry => entry.Text));
    }

    [Fact]
    public void Preview_NormalizesWhitespaceAndUsesAtMostThreeWords()
    {
        Assert.Equal(new CalendarEventPreview("one two three…", true),
            _preview.Generate("  one\t two\r\nthree   four "));
        Assert.Equal(new CalendarEventPreview("one two three", false),
            _preview.Generate("one two three"));
        Assert.Equal(new CalendarEventPreview("one two", false),
            _preview.Generate("  one\t two\r\n "));
    }

    [Fact]
    public void Preview_UsesAtMostThirtyTwoCharactersAndHandlesOneLongWord()
    {
        var result = _preview.Generate(new string('x', 50));

        Assert.True(result.IsTruncated);
        Assert.Equal(32, result.Text.Length);
        Assert.Equal(new string('x', 31) + "…", result.Text);
    }

    [Fact]
    public void Preview_SupportsHebrewContent()
    {
        Assert.Equal(new CalendarEventPreview("פגישה עם צוות…", true),
            _preview.Generate("פגישה עם צוות המוצר"));
    }

    [Fact]
    public void FullPreview_UsesSevenWordsAndItsLargerCharacterCap()
    {
        const string sevenWords = "one two three four five six seven";
        var eightWords = sevenWords + " eight";

        Assert.Equal(new CalendarEventPreview(sevenWords, false),
            _preview.Generate(sevenWords, CalendarLayoutMode.Large));
        Assert.Equal(new CalendarEventPreview(sevenWords + "…", true),
            _preview.Generate(eightWords, CalendarLayoutMode.Large));

        var characterCapped = _preview.Generate(
            string.Join(' ', Enumerable.Repeat("abcdefghijkl", 7)),
            CalendarLayoutMode.Large);
        Assert.True(characterCapped.IsTruncated);
        Assert.Equal(CalendarEventPreviewGenerator.LargeMaximumCharacters, characterCapped.Text.Length);
        Assert.EndsWith("…", characterCapped.Text);
    }

    private static CalendarEntry Entry(DateOnly date, string text) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        Text = text
    };
}
