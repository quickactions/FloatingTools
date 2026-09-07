using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.SharedUi.Controls;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class NotesTitleDirectionRuntimeTests
{
    [Theory]
    [InlineData("שלום", true)]
    [InlineData("3 דברים למחר", true)]
    [InlineData("Hello", false)]
    [InlineData("2026 Goals", false)]
    [InlineData("2026", false)]
    public void ActualHeaderAndMenuTitles_FollowDirectionAndPhysicalEdge(string title, bool rtl)
        => WpfTestApplication.Run(() =>
        {
            var context = new TitleContext();
            var view = new NotesToolView { DataContext = context };
            var window = new Window { Content = view, Width = 300, Height = 458, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                // Change the title after loading, so converter bindings must update.
                context.Note.Title = title;
                context.RefreshTitle();
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                var header = Descendants(view).OfType<ToolHeaderControl>().Single();
                var headerTitle = Descendants(header).OfType<TextBlock>().Single(t => t.Text == title);
                var menuTitle = Descendants(view).OfType<TextBlock>().Single(t => t.Text == title && ReferenceEquals(t.DataContext, context.Note));
                foreach (var block in new[] { headerTitle, menuTitle })
                {
                    Assert.Equal(rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, block.FlowDirection);
                    // RTL mirrors the presenter; logical Left is physical right.
                    Assert.Equal(TextAlignment.Left, block.TextAlignment);
                    block.Foreground = Brushes.White;
                    var ink = GetLastLineInk(block);
                    Assert.True(rtl ? ink.RightMargin < ink.LeftMargin : ink.LeftMargin < ink.RightMargin,
                        $"Title {title}: physical margins left={ink.LeftMargin}, right={ink.RightMargin}.");
                }
            }
            finally { window.Close(); }
        });

    [Fact]
    public void SharedHeader_DefaultTitlePresentationMatchesPreviousBehavior()
        => WpfTestApplication.Run(() =>
        {
            var header = new ToolHeaderControl
            {
                Title = "Other tool",
                Style = (Style)Application.Current.FindResource("FloatingToolsSharedToolHeaderStyle")
            };
            var window = new Window { Content = header, Width = 300, Height = 100, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.Equal(FlowDirection.LeftToRight, header.TitleFlowDirection);
                Assert.Equal(TextAlignment.Left, header.TitleTextAlignment);
                Assert.Equal(new Thickness(), header.TitleMargin);
                Assert.Equal(HorizontalAlignment.Center, header.TitleHorizontalAlignment);
                var title = Descendants(header).OfType<TextBlock>().Single(t => t.Text == header.Title);
                Assert.Equal(FlowDirection.LeftToRight, title.FlowDirection);
                Assert.Equal(TextAlignment.Left, title.TextAlignment);
                Assert.Equal(HorizontalAlignment.Center, title.HorizontalAlignment);
            }
            finally { window.Close(); }
        });

    public sealed class TitleContext : INotifyPropertyChanged
    {
        public NoteDocument Note { get; } = new() { Title = "Initial" };
        public string ActiveTitle => Note.Title;
        public NoteDocument[] FilteredNotes => [Note];
        public bool IsMenuOpen { get; set; } = true;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void RefreshTitle() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveTitle)));
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
            && pixels[offset] > 140
            && pixels[offset + 1] > 140
            && pixels[offset + 2] > 140;
    }

    private sealed record LastLineInk(int LineCount, int LeftMargin, int RightMargin);
}
