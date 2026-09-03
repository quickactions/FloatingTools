namespace FloatingTools.App.Models;

public sealed class TranslationEntry
{
    public required Guid Id { get; init; }

    public required string SourceText { get; init; }

    public required TranslationResult Result { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public bool IsFavorite { get; set; }

    public int UsageCount { get; set; }

    public required DateTimeOffset LastUsedAt { get; set; }
}
