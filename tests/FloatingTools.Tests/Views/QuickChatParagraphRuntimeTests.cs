using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;
using Xunit.Abstractions;

namespace FloatingTools.Tests.Views;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class QuickChatParagraphRuntimeTests
{
    private readonly ITestOutputHelper _output;

    public QuickChatParagraphRuntimeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void RealMessageTemplateStretchesOuterContainersAndAnchorsWrappedTextInk()
        => RunSta(() =>
        {
            var session = new PresentationSession(
                User(HebrewWrappingText),
                Assistant(HebrewWrappingText),
                User(EnglishWrappingText),
                Assistant(EnglishWrappingText));
            var viewModel = new QuickChatViewModel(session, new PresentationImageStore());
            viewModel.InitializeAsync().GetAwaiter().GetResult();
            var view = new QuickChatToolView { DataContext = viewModel };
            var window = new Window
            {
                Content = view,
                Width = 360,
                Height = 900,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                DrainDispatcher();
                window.UpdateLayout();

                var conversationViewport = FindDescendants<ScrollViewer>(view)
                    .Single(element => element.Name == "ConversationScrollViewer");
                var messageItemsControl = FindDescendants<ItemsControl>(view)
                    .Single(element => element.Name == "MessageItemsControl");
                var paragraphs = FindDescendants<TextBox>(view)
                    .Where(element => element.DataContext is QuickChatParagraphPresentation)
                    .ToArray();
                Assert.Equal(4, paragraphs.Length);

                paragraphs[0].Select(0, 8);
                Assert.Equal(HebrewWrappingText[..8], paragraphs[0].SelectedText);
                paragraphs[2].Select(0, 8);
                Assert.Equal(EnglishWrappingText[..8], paragraphs[2].SelectedText);

                for (var index = 0; index < paragraphs.Length; index++)
                {
                    var paragraph = paragraphs[index];
                    var paragraphPresenter = FindAncestor<ContentPresenter>(paragraph, _ => true);
                    var paragraphItemsControl = FindAncestor<ItemsControl>(paragraph,
                        element => !ReferenceEquals(element, messageItemsControl));
                    var messageSurface = FindAncestor<Border>(paragraph,
                        element => element.Name == "MessageSurface");
                    var messageHoverArea = FindAncestor<Grid>(paragraph,
                        element => element.Name == "MessageHoverArea");
                    var outerPresenter = FindAncestor<ContentPresenter>(messageHoverArea!, _ => true);

                    Assert.NotNull(paragraphPresenter);
                    Assert.NotNull(paragraphItemsControl);
                    Assert.NotNull(messageSurface);
                    Assert.NotNull(messageHoverArea);
                    Assert.NotNull(outerPresenter);

                    var role = index is 0 or 2 ? "user" : "assistant";
                    var language = index < 2 ? "Hebrew" : "English";
                    WriteGeometry($"{role} {language} outer", outerPresenter!, conversationViewport);
                    WriteGeometry($"{role} {language} hover", messageHoverArea!, conversationViewport);
                    WriteGeometry($"{role} {language} surface", messageSurface!, conversationViewport);
                    WriteGeometry($"{role} {language} paragraphs", paragraphItemsControl!, conversationViewport);
                    WriteGeometry($"{role} {language} paragraph presenter", paragraphPresenter!, conversationViewport);
                    WriteGeometry($"{role} {language} text", paragraph, conversationViewport);

                    AssertParagraph(paragraph,
                        language == "Hebrew" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                        TextAlignment.Left);
                    Assert.Equal(HorizontalAlignment.Stretch, outerPresenter!.HorizontalAlignment);
                    Assert.Equal(HorizontalAlignment.Stretch,
                        messageItemsControl.HorizontalContentAlignment);
                    AssertClose(messageItemsControl.ActualWidth, outerPresenter.ActualWidth, 1);
                    AssertClose(outerPresenter.ActualWidth - messageHoverArea!.Margin.Left
                        - messageHoverArea.Margin.Right, messageHoverArea.ActualWidth, 1);

                    var ink = GetLastLineInk(paragraph, messageSurface!);
                    _output.WriteLine(
                        $"{role} {language} last ink: lines={ink.LineCount}, left={ink.Left}, right={ink.Right}, " +
                        $"leftMargin={ink.LeftMargin}, rightMargin={ink.RightMargin}");
                    Assert.True(ink.LineCount >= 3,
                        $"Expected wrapped {language} text to occupy at least three visual lines.");
                    if (language == "Hebrew")
                    {
                        Assert.True(ink.RightMargin < ink.LeftMargin,
                            $"Expected the final Hebrew line to be right-anchored, but margins were " +
                            $"left={ink.LeftMargin}, right={ink.RightMargin}.");
                    }
                    else
                    {
                        Assert.True(ink.LeftMargin < ink.RightMargin,
                            $"Expected the final English line to be left-anchored, but margins were " +
                            $"left={ink.LeftMargin}, right={ink.RightMargin}.");
                    }
                }
            }
            finally
            {
                window.Close();
            }
        });

    private const string HebrewWrappingText =
        "זהו טקסט ארוך בעברית שנועד להישבר למספר שורות בתוך חלון השיחה כדי לבדוק כיוון ויישור בצורה אמינה ועקבית בכל מצב. סוף";

    private const string EnglishWrappingText =
        "This is a long English message intended to wrap across several visual lines in the conversation window so alignment can be checked reliably. End";

    private static void AssertParagraph(
        TextBox paragraph,
        FlowDirection expectedFlow,
        TextAlignment expectedAlignment)
    {
        var messageSurface = FindAncestor<Border>(
            paragraph,
            element => element.Name == "MessageSurface");
        Assert.NotNull(messageSurface);
        Assert.Equal(expectedFlow, paragraph.FlowDirection);
        Assert.Equal(expectedAlignment, paragraph.TextAlignment);
        Assert.Equal(HorizontalAlignment.Stretch, paragraph.HorizontalAlignment);
        Assert.True(paragraph.ActualWidth > 250,
            $"Paragraph width was {paragraph.ActualWidth}; message surface width was {messageSurface!.ActualWidth}.");
        Assert.True(Math.Abs(paragraph.ActualWidth
            - (messageSurface!.ActualWidth - messageSurface.Padding.Left
                - messageSurface.Padding.Right)) < 1);
    }

    private static QuickChatMessage Assistant(string text) => new()
    {
        Id = Guid.NewGuid(),
        Role = QuickChatMessageRole.Assistant,
        Text = text,
        Status = QuickChatMessageStatus.Completed,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static QuickChatMessage User(string text) => new()
    {
        Id = Guid.NewGuid(),
        Role = QuickChatMessageRole.User,
        Text = text,
        Status = QuickChatMessageStatus.Completed,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private void WriteGeometry(string label, FrameworkElement element, Visual viewport)
    {
        var bounds = element.TransformToAncestor(viewport)
            .TransformBounds(new Rect(element.RenderSize));
        var horizontalContentAlignment = element switch
        {
            Control control => control.HorizontalContentAlignment.ToString(),
            _ => "n/a"
        };
        var text = element as TextBlock;
        _output.WriteLine(
            $"{label}: width={element.ActualWidth:F2}, height={element.ActualHeight:F2}, " +
            $"left={bounds.Left:F2}, right={bounds.Right:F2}, " +
            $"HA={element.HorizontalAlignment}, HCA={horizontalContentAlignment}, " +
            $"flow={element.FlowDirection}, alignment={text?.TextAlignment.ToString() ?? "n/a"}");
    }

    private static LastLineInk GetLastLineInk(
        TextBox paragraph,
        FrameworkElement renderRoot)
    {
        var width = Math.Max(1, (int)Math.Ceiling(renderRoot.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(renderRoot.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(renderRoot);
        var stride = width * 4;
        var pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        var paragraphBounds = paragraph.TransformToAncestor(renderRoot)
            .TransformBounds(new Rect(paragraph.RenderSize));
        var cropLeft = Math.Max(0, (int)Math.Floor(paragraphBounds.Left));
        var cropTop = Math.Max(0, (int)Math.Floor(paragraphBounds.Top));
        var cropRight = Math.Min(width - 1, (int)Math.Ceiling(paragraphBounds.Right) - 1);
        var cropBottom = Math.Min(height - 1, (int)Math.Ceiling(paragraphBounds.Bottom) - 1);
        var cropWidth = cropRight - cropLeft + 1;
        var cropHeight = cropBottom - cropTop + 1;

        var occupiedRows = new bool[cropHeight];
        for (var y = 0; y < cropHeight; y++)
        {
            for (var x = 0; x < cropWidth; x++)
            {
                if (IsForegroundPixel(pixels, stride, cropLeft + x, cropTop + y))
                {
                    occupiedRows[y] = true;
                    break;
                }
            }
        }

        var bands = new List<(int Top, int Bottom)>();
        for (var y = 0; y < cropHeight; y++)
        {
            if (!occupiedRows[y])
            {
                continue;
            }

            var top = y;
            while (y + 1 < cropHeight && occupiedRows[y + 1])
            {
                y++;
            }

            bands.Add((top, y));
        }

        Assert.NotEmpty(bands);
        var last = bands[^1];
        var left = cropWidth;
        var right = -1;
        for (var y = last.Top; y <= last.Bottom; y++)
        {
            for (var x = 0; x < cropWidth; x++)
            {
                if (!IsForegroundPixel(pixels, stride, cropLeft + x, cropTop + y))
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        Assert.True(right >= left);
        return new LastLineInk(bands.Count, left, right, left, cropWidth - right - 1);
    }

    private static bool IsForegroundPixel(byte[] pixels, int stride, int x, int y)
    {
        var offset = (y * stride) + (x * 4);
        return pixels[offset + 3] > 32
            && pixels[offset] > 140
            && pixels[offset + 1] > 140
            && pixels[offset + 2] > 140;
    }

    private static void AssertClose(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance,
            $"Expected {expected:F2} (+/- {tolerance:F2}), actual {actual:F2}.");

    private sealed record LastLineInk(
        int LineCount,
        int Left,
        int Right,
        int LeftMargin,
        int RightMargin);

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static T? FindAncestor<T>(
        DependencyObject child,
        Func<T, bool> predicate)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(child);
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match && predicate(match))
            {
                return match;
            }
        }

        return null;
    }

    private static void DrainDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));

    private static void RunSta(Action action) => WpfTestApplication.Run(action);

    private sealed class PresentationSession(params QuickChatMessage[] messages)
        : IActiveQuickChatConversation
    {
        public event EventHandler<QuickChatConversationChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }

        public QuickChatConversationState CurrentState { get; } = new()
        {
            Messages = [.. messages]
        };

        public IReadOnlyList<QuickChatMessage> Messages => CurrentState.Messages;

        public string? AdditionalInstructions => null;

        public bool IsGenerating => false;

        public bool IsInitialized { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(string? text, IReadOnlyList<QuickChatAttachment>? attachments = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RetryAsync(Guid assistantMessageId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetAdditionalInstructionsAsync(string? additionalInstructions,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteUnsentAttachmentAsync(string assetFileName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NewChatAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PrepareForExitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class PresentationImageStore : IQuickChatImageStore
    {
        public Task<ManagedQuickChatImage> ImportFileAsync(
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ManagedQuickChatImage> ImportBytesAsync(
            ReadOnlyMemory<byte> imageBytes,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string GetAbsolutePath(string assetFileName) => assetFileName;

        public IReadOnlyList<string> GetManagedAssetFileNames() => [];

        public Task DeleteAsync(string assetFileName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
