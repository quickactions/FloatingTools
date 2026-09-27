using System.Windows;
using System.Windows.Controls;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class TopDockContentWidthPolicyTests
{
    [Fact]
    public void ReadingWidths_AreOptInAndLeaveInteractiveSurfacesUnconstrained()
        => WpfTestApplication.Run(() =>
        {
            var defaultStyle = (Style)Application.Current.FindResource("TopDockDefaultReadingWidthStyle");
            var compactStyle = (Style)Application.Current.FindResource("TopDockCompactReadingWidthStyle");
            Assert.Equal(400d, (double)Application.Current.FindResource("TopDockDefaultReadingMaxWidth"));
            Assert.Equal(340d, (double)Application.Current.FindResource("TopDockCompactReadingMaxWidth"));
            Assert.Equal(new Thickness(16, 0, 16, 0),
                (Thickness)Application.Current.FindResource("TopDockReadingHorizontalMargin"));

            var reading = new Border { Style = defaultStyle };
            var compact = new Border { Style = compactStyle };
            var ordinary = new Border();
            var editor = new TextBox();
            var scroller = new ScrollViewer();
            var splitter = new GridSplitter();

            Assert.Equal(400, reading.MaxWidth);
            Assert.Equal(340, compact.MaxWidth);
            Assert.Equal(HorizontalAlignment.Center, reading.HorizontalAlignment);
            Assert.Equal(new Thickness(16, 0, 16, 0), reading.Margin);
            Assert.True(double.IsPositiveInfinity(ordinary.MaxWidth));
            Assert.True(double.IsPositiveInfinity(editor.MaxWidth));
            Assert.True(double.IsPositiveInfinity(scroller.MaxWidth));
            Assert.True(double.IsPositiveInfinity(splitter.MaxWidth));

            var host = new Grid { Width = 460, Height = 40 };
            reading.Child = new Border { Width = 600, Height = 20 };
            host.Children.Add(reading);
            host.Measure(new Size(460, 40));
            host.Arrange(new Rect(0, 0, 460, 40));
            Assert.Equal(400, reading.ActualWidth);
            Assert.Equal(30, reading.TranslatePoint(new Point(), host).X);

            var wideHost = new Grid { Width = 720, Height = 40 };
            compact.Child = new Border { Width = 600, Height = 20 };
            wideHost.Children.Add(compact);
            wideHost.Measure(new Size(720, 40));
            wideHost.Arrange(new Rect(0, 0, 720, 40));
            Assert.Equal(340, compact.ActualWidth);
            Assert.Equal(190, compact.TranslatePoint(new Point(), wideHost).X);
        });
}
