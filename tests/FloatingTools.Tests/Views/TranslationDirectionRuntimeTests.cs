using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.Views;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class TranslationDirectionRuntimeTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void RealTranslationSourceAndResultTextAnchorWrappedInkOnThePhysicalLanguageSide(
        bool hebrew,
        bool sourceSurface)
        => WpfTestApplication.Run(() =>
        {
            var text = hebrew ? HebrewWrappingText : EnglishWrappingText;
            var entry = CreateEntryViewModel(text);
            var textBox = new TextBox
            {
                Width = 280,
                Height = 150,
                Padding = new Thickness(8),
                Background = Brushes.Black,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Text = sourceSurface ? entry.SourceText : entry.MainTranslation,
                FlowDirection = sourceSurface
                    ? entry.SourceFlowDirection
                    : entry.ResultFlowDirection,
                TextAlignment = sourceSurface
                    ? entry.SourceTextAlignment
                    : entry.ResultTextAlignment
            };
            var window = new Window
            {
                Content = textBox,
                Width = 300,
                Height = 180,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Background = Brushes.Black
            };

            try
            {
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(() => { }));
                window.UpdateLayout();

                var ink = GetLastLineInk(textBox);
                Assert.True(ink.LineCount >= 3,
                    $"Expected wrapped text to occupy at least three lines, got {ink.LineCount}.");
                Assert.Equal(
                    hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                    textBox.FlowDirection);
                Assert.Equal(TextAlignment.Left, textBox.TextAlignment);
                if (hebrew)
                {
                    Assert.True(ink.RightMargin < ink.LeftMargin,
                        $"Expected Hebrew ink on the physical right; margins were left={ink.LeftMargin}, right={ink.RightMargin}.");
                }
                else
                {
                    Assert.True(ink.LeftMargin < ink.RightMargin,
                        $"Expected English ink on the physical left; margins were left={ink.LeftMargin}, right={ink.RightMargin}.");
                }
            }
            finally
            {
                window.Close();
            }
        });

    private static TranslationEntryViewModel CreateEntryViewModel(string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new TranslationEntryViewModel(
            new TranslationEntry
            {
                Id = Guid.NewGuid(),
                SourceText = text,
                Result = new TranslationResult(text),
                CreatedAt = now,
                LastUsedAt = now,
                UsageCount = 1
            },
            new NullClipboardService(),
            new InMemoryTranslationHistoryStore());
    }

    private const string HebrewWrappingText =
        "זהו טקסט ארוך בעברית שנועד להישבר למספר שורות בתוך תוצאת התרגום כדי לבדוק כיוון ויישור בצורה אמינה ועקבית. סוף";

    private const string EnglishWrappingText =
        "This is long English text intended to wrap across several visual lines in the translation feed so physical alignment can be verified reliably. End";

    private static LastLineInk GetLastLineInk(TextBox textBox)
    {
        var width = Math.Max(1, (int)Math.Ceiling(textBox.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(textBox.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(textBox);
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var occupiedRows = new bool[height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (IsForegroundPixel(pixels, stride, x, y))
                {
                    occupiedRows[y] = true;
                    break;
                }
            }
        }

        var bands = new List<(int Top, int Bottom)>();
        for (var y = 0; y < height; y++)
        {
            if (!occupiedRows[y])
            {
                continue;
            }

            var top = y;
            while (y + 1 < height && occupiedRows[y + 1])
            {
                y++;
            }

            bands.Add((top, y));
        }

        Assert.NotEmpty(bands);
        var last = bands[^1];
        var left = width;
        var right = -1;
        for (var y = last.Top; y <= last.Bottom; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsForegroundPixel(pixels, stride, x, y))
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        Assert.True(right >= left);
        return new LastLineInk(bands.Count, left, width - right - 1);
    }

    private static bool IsForegroundPixel(byte[] pixels, int stride, int x, int y)
    {
        var offset = (y * stride) + (x * 4);
        return pixels[offset + 3] > 32
            && pixels[offset] > 140
            && pixels[offset + 1] > 140
            && pixels[offset + 2] > 140;
    }

    private sealed record LastLineInk(int LineCount, int LeftMargin, int RightMargin);
}
