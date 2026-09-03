using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class CalendarEntrySearchServiceTests
{
    private readonly CalendarEntrySearchService _service = new();
    private readonly DateOnly _today = new(2026, 9, 1);

    [Fact]
    public void Search_MatchesCaseInsensitiveEnglishAndHebrewText()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 9, 2), "Project REVIEW"),
            Entry(new DateOnly(2026, 9, 3), "פגישה עם דנה")
        };

        Assert.Single(_service.Search(entries, "review", _today));
        Assert.Single(_service.Search(entries, "פגישה", _today));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("missing")]
    public void Search_EmptyOrMissingQueryReturnsNoEntries(string? query)
    {
        Assert.Empty(_service.Search(
            [Entry(_today, "present")],
            query,
            _today));
    }

    [Fact]
    public void Search_OrdersCurrentAndFutureAscendingThenOlderMostRecentFirst()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 8, 1), "match old"),
            Entry(new DateOnly(2026, 9, 3), "match future"),
            Entry(new DateOnly(2026, 8, 31), "match recent"),
            Entry(_today, "match today"),
            Entry(new DateOnly(2026, 9, 2), "match tomorrow")
        };

        var results = _service.Search(entries, "MATCH", _today);

        Assert.Equal(
            [_today, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3),
             new DateOnly(2026, 8, 31), new DateOnly(2026, 8, 1)],
            results.Select(entry => entry.Date));
    }

    [Fact]
    public void Search_AcceptsOnlyUserEntryDomainObjects()
    {
        var method = typeof(CalendarEntrySearchService).GetMethod(nameof(
            CalendarEntrySearchService.Search));

        Assert.NotNull(method);
        Assert.Equal(typeof(IEnumerable<CalendarEntry>),
            method!.GetParameters()[0].ParameterType);
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            parameter.ParameterType.Name.Contains("Holiday", StringComparison.Ordinal));
    }

    private static CalendarEntry Entry(DateOnly date, string text) => new()
    {
        Date = date,
        Text = text
    };
}
