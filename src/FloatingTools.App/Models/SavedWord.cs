namespace FloatingTools.App.Models;

public sealed class SavedWord
{
    public required Guid Id { get; init; }

    public required string SourceText { get; init; }

    public required string PrimaryTranslation { get; init; }

    public required string SourceLanguage { get; init; }

    public required string TargetLanguage { get; init; }

    public required DateTimeOffset SavedAt { get; init; }
}
