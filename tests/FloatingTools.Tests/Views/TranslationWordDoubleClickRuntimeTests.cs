using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class TranslationWordDoubleClickRuntimeTests
{
    [Theory]
    [InlineData(true, "apply", "I'm going to apply for a job.")]
    [InlineData(false, "apply", "I want to apply for a job.")]
    public void PointerOnEnglishWordCreatesRequestWithClickedSurfaceContext(
        bool sourceSurface,
        string expectedWord,
        string expectedContext)
    {
        WpfTestApplication.Run(() =>
        {
            var entry = CreateEntry(sourceSurface);
            var textBox = new TextBox
            {
                Text = sourceSurface ? entry.SourceText : entry.MainTranslation,
                Tag = sourceSurface ? "Source" : "Translation",
                DataContext = entry,
                IsReadOnly = true,
                Width = 420,
                Height = 50
            };
            using var host = Show(textBox);
            var wordIndex = textBox.Text.IndexOf("apply", StringComparison.Ordinal) + 2;
            var point = Center(textBox.GetRectFromCharacterIndex(wordIndex));
            textBox.Select(wordIndex - 1, 3);
            var originalSelection = (textBox.SelectionStart, textBox.SelectionLength);

            var created = TranslationToolView.TryCreateContextualWordTranslationRequest(
                textBox,
                point,
                out var request);

            Assert.True(created);
            Assert.NotNull(request);
            Assert.Equal(expectedWord, request.Word);
            Assert.Equal(expectedContext, request.Context);
            Assert.Equal(originalSelection, (textBox.SelectionStart, textBox.SelectionLength));
        });
    }

    [Fact]
    public void PointerOnHebrewDoesNotCreateRequest()
    {
        WpfTestApplication.Run(() =>
        {
            var entry = CreateEntry();
            var textBox = new TextBox
            {
                Text = entry.SourceText,
                Tag = "Source",
                DataContext = entry,
                IsReadOnly = true,
                Width = 420,
                Height = 50
            };
            using var host = Show(textBox);
            var point = Center(textBox.GetRectFromCharacterIndex(1));

            Assert.False(TranslationToolView.TryCreateContextualWordTranslationRequest(
                textBox,
                point,
                out var request));
            Assert.Null(request);
        });
    }

    private static TranslationEntryViewModel CreateEntry(bool sourceSurface = false)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new TranslationEntry
        {
            Id = Guid.NewGuid(),
            SourceText = sourceSurface
                ? "I'm going to apply for a job."
                : "אני רוצה apply עכשיו",
            Result = sourceSurface
                ? new TranslationResult(
                    "אני הולכת להגיש מועמדות לעבודה.",
                    "en",
                    "Test",
                    targetLanguage: "he")
                : new TranslationResult(
                    "I want to apply for a job.",
                    "he",
                    "Test",
                    targetLanguage: "en"),
            CreatedAt = now,
            LastUsedAt = now,
            UsageCount = 1
        };
        return new TranslationEntryViewModel(
            entry,
            new NullClipboardService(),
            new InMemoryTranslationHistoryStore());
    }

    private static WindowScope Show(TextBox textBox)
    {
        var window = new Window
        {
            Width = 480,
            Height = 120,
            Content = textBox,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        window.Show();
        textBox.UpdateLayout();
        return new WindowScope(window);
    }

    private static Point Center(Rect rect) =>
        new(rect.Left + Math.Max(1, rect.Width / 2), rect.Top + Math.Max(1, rect.Height / 2));

    private sealed class WindowScope(Window window) : IDisposable
    {
        public void Dispose() => window.Close();
    }

    private sealed class NullClipboardService : IClipboardService
    {
        public void SetText(string text)
        {
        }
    }
}
