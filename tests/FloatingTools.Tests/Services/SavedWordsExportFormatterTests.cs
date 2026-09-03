using FloatingTools.App.Models;
using FloatingTools.App.Services;
using PdfSharp.Pdf.IO;

namespace FloatingTools.Tests.Services;

public sealed class SavedWordsExportFormatterTests
{
    [Fact]
    public void HebrewToEnglish_IsNormalizedToEnglishThenHebrew()
    {
        var item = CreateItem("כדור", "ball", "he", "en");

        var row = SavedWordExportNormalizer.Normalize(item);

        Assert.Equal("ball", row.English);
        Assert.Equal("כדור", row.Hebrew);
    }

    [Fact]
    public void EnglishToHebrew_IsNormalizedToEnglishThenHebrew()
    {
        var item = CreateItem("bank", "בנק", "en", "he");

        var row = SavedWordExportNormalizer.Normalize(item);

        Assert.Equal("bank", row.English);
        Assert.Equal("בנק", row.Hebrew);
    }

    [Fact]
    public void MixedDirections_PreserveInputOrderAndNormalizeEveryPair()
    {
        var newest = CreateItem("כדור", "ball", "he", "en", minutesAgo: 0);
        var older = CreateItem("bank", "בנק", "en", "he", minutesAgo: 1);

        var rows = SavedWordExportNormalizer.Normalize([newest, older]);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("ball", row.English);
                Assert.Equal("כדור", row.Hebrew);
            },
            row =>
            {
                Assert.Equal("bank", row.English);
                Assert.Equal("בנק", row.Hebrew);
            });
    }

    [Fact]
    public void Csv_UsesEnglishHebrewSavedAtHeaderAndEscapesContent()
    {
        var item = CreateItem(
            "a,\"quoted\"\nline",
            "תרגום, נוסף",
            "en",
            "he");

        var csv = SavedWordsExportFormatter.CreateCsv([item]);

        Assert.StartsWith("English,Hebrew,SavedAt\r\n", csv);
        Assert.Contains("\"a,\"\"quoted\"\"\nline\"", csv);
        Assert.Contains("\"תרגום, נוסף\"", csv);
    }

    [Fact]
    public void Csv_WithoutMetadata_UsesOnlyEnglishHebrewColumns()
    {
        var csv = SavedWordsExportFormatter.CreateCsv(
            [CreateItem("כדור", "ball", "he", "en")],
            includeSavedAt: false);

        Assert.Equal(
            "English,Hebrew\r\n\"ball\",\"כדור\"\r\n",
            csv);
    }

    [Fact]
    public void Text_AlwaysUsesEnglishEmDashHebrewAndPreservesUnicode()
    {
        var text = SavedWordsExportFormatter.CreateText(
            [
                CreateItem("כדור", "ball", "he", "en"),
                CreateItem("bank", "בנק", "en", "he")
            ]);

        Assert.Equal(
            $"ball — כדור{Environment.NewLine}bank — בנק",
            text);
    }

    [Fact]
    public void Csv_WithTwoHundredRows_GeneratesEveryVocabularyPair()
    {
        var items = Enumerable.Range(1, 200)
            .Select(index => CreateItem(
                $"word-{index}",
                $"מילה-{index}",
                "en",
                "he",
                minutesAgo: index))
            .ToArray();

        var csv = SavedWordsExportFormatter.CreateCsv(items);

        Assert.Equal(201, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("\"word-1\",\"מילה-1\"", csv);
        Assert.Contains("\"word-200\",\"מילה-200\"", csv);
    }

    [Fact]
    public void Text_WithTwoHundredRows_GeneratesEveryVocabularyPair()
    {
        var items = Enumerable.Range(1, 200)
            .Select(index => CreateItem(
                $"word-{index}",
                $"מילה-{index}",
                "en",
                "he",
                minutesAgo: index))
            .ToArray();

        var text = SavedWordsExportFormatter.CreateText(items);

        Assert.Equal(200, text.Split(Environment.NewLine).Length);
        Assert.StartsWith("word-1 — מילה-1", text);
        Assert.EndsWith("word-200 — מילה-200", text);
    }

    [Fact]
    public void Pdf_WithTwoHundredMixedDirectionRows_IsValidAndPaginates()
    {
        var items = Enumerable.Range(1, 200)
            .Select(index => index % 2 == 0
                ? CreateItem($"מילה-{index}", $"word-{index}", "he", "en", index)
                : CreateItem($"word-{index}", $"מילה-{index}", "en", "he", index))
            .ToArray();

        var pdf = RunInSta(() => SavedWordsPdfExporter.Create(items));

        Assert.True(pdf.AsSpan().StartsWith("%PDF"u8));
        using var stream = new MemoryStream(pdf);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(document.PageCount > 1);
    }

    [Fact]
    public void Pdf_CustomTitle_IsStoredInDocumentMetadata()
    {
        var pdf = RunInSta(() => SavedWordsPdfExporter.Create(
            [CreateItem("bank", "בנק", "en", "he")],
            "Frequent Words"));

        using var stream = new MemoryStream(pdf);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.Equal("Frequent Words", document.Info.Title);
    }

    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw new Xunit.Sdk.XunitException(
                $"STA PDF generation failed: {exception}");
        }

        return result!;
    }

    private static SavedWord CreateItem(
        string source,
        string translation,
        string sourceLanguage,
        string targetLanguage,
        int minutesAgo = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceText = source,
            PrimaryTranslation = translation,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            SavedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo)
        };
}
