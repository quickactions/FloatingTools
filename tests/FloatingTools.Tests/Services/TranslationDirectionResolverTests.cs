using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class TranslationDirectionResolverTests
{
    [Theory]
    [InlineData("שלום", "he", "en")]
    [InlineData("123 שלום world", "he", "en")]
    [InlineData("Repository", "en", "he")]
    [InlineData("123 !?", "en", "he")]
    public void Resolve_UsesHebrewPresenceAsTemporaryDirectionRule(
        string text,
        string expectedSource,
        string expectedTarget)
    {
        var result = TranslationDirectionResolver.Resolve(text);

        Assert.Equal(expectedSource, result.SourceLanguage);
        Assert.Equal(expectedTarget, result.TargetLanguage);
    }

    [Theory]
    [InlineData(TranslationLanguageMode.HebrewToEnglish, "English input", "he", "en")]
    [InlineData(TranslationLanguageMode.EnglishToHebrew, "טקסט עברי", "en", "he")]
    public void Resolve_FixedModeOverridesTextDetection(
        TranslationLanguageMode mode,
        string text,
        string expectedSource,
        string expectedTarget)
    {
        var result = TranslationDirectionResolver.Resolve(text, mode);

        Assert.Equal(expectedSource, result.SourceLanguage);
        Assert.Equal(expectedTarget, result.TargetLanguage);
    }
}
