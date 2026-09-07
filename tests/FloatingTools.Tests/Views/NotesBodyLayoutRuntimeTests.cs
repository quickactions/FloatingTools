using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class NotesBodyLayoutRuntimeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActualBody_KeepsUniformSpacesAndPhysicalAlignmentAcrossResize(bool hebrew)
        => WpfTestApplication.Run(() =>
        {
            var text = string.Join(" ", Enumerable.Repeat(hebrew ? "שלום עולם דברים למחר בדיקה" : "Hello world things for tomorrow", 12));
            var view = new NotesToolView
            {
                DataContext = new BodyContext { ActiveBlocks = [new TextNoteBlock { Text = text }] }
            };
            var window = new Window
            {
                Content = view, Width = 300, Height = 458,
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = Brushes.Transparent, SnapsToDevicePixels = true,
                ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000
            };
            try
            {
                window.Show();
                foreach (var width in new[] { 300d, 560d, 300d })
                {
                    window.Width = width;
                    window.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                    var editor = Descendants(view).OfType<NoteDirectionalTextBox>().Single(e => e.Name == "TextBlockEditor");
                    Assert.Equal(text, editor.Text);
                    Assert.Equal(width - 16, editor.ActualWidth, 1);
                    Assert.True(editor.LineCount >= 3);
                    var spaces = new List<double>();
                    var linesWithSpaces = new HashSet<int>();
                    for (var i = 1; i < text.Length - 1; i++)
                    {
                        if (text[i] != ' ') continue;
                        var line = editor.GetLineIndexFromCharacterIndex(i);
                        if (line != editor.GetLineIndexFromCharacterIndex(i + 1)) continue;
                        var before = editor.GetRectFromCharacterIndex(i, false);
                        var after = editor.GetRectFromCharacterIndex(i, true);
                        spaces.Add(Math.Abs(after.X - before.X));
                        linesWithSpaces.Add(line);
                    }
                    Assert.True(linesWithSpaces.Count >= 3);
                    Assert.True(spaces.Min() > 0);
                    Assert.True(spaces.Max() - spaces.Min() < .02,
                        $"Panel {width}: space advances varied from {spaces.Min()} to {spaces.Max()} DIP.");
                    var ink = GetLastLineInk(editor);
                    Assert.Equal(hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, editor.FlowDirection);
                    Assert.True(hebrew ? ink.RightMargin < ink.LeftMargin : ink.LeftMargin < ink.RightMargin,
                        $"Panel {width}, Hebrew={hebrew}: physical ink margins left={ink.LeftMargin}, right={ink.RightMargin}.");
                }
            }
            finally { window.Close(); }
        });

    public sealed class BodyContext
    {
        public bool IsMenuOpen { get; set; }
        public string ActiveTitle => "Body test";
        public TextNoteBlock[] ActiveBlocks { get; set; } = [];
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static LastLineInk GetLastLineInk(FrameworkElement textBox)
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
            && pixels[offset] < 100
            && pixels[offset + 1] < 100
            && pixels[offset + 2] < 100;
    }

    private sealed record LastLineInk(int LineCount, int LeftMargin, int RightMargin);
}
