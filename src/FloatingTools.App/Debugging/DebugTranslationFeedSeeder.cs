#if DEBUG
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Debugging;

internal static class DebugTranslationFeedSeeder
{
    public static void Seed(TranslationToolViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        Add(viewModel, "שלום", "Hello", createdAt);
        Add(viewModel, "Repository", "מאגר", createdAt.AddMinutes(1));
        Add(
            viewModel,
            "אני צריך להגיש את הפרויקט",
            "I need to submit the project",
            createdAt.AddMinutes(2));
    }

    private static void Add(
        TranslationToolViewModel viewModel,
        string sourceText,
        string translation,
        DateTimeOffset createdAt)
    {
        viewModel.AddDebugPreviewEntry(new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = sourceText,
            Result = new TranslationResult(
                translation,
                detectedLanguage: TranslationDirectionResolver.Resolve(sourceText)
                    .SourceLanguage,
                provider: "Debug preview"),
            CreatedAt = createdAt,
            IsFavorite = false,
            UsageCount = 0,
            LastUsedAt = createdAt
        });
    }
}
#endif
