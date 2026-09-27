using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Xml.Linq;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class TopDockStageBUiTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void TranslationMenu_OptsIntoExistingSharedScrollViewerStyle()
    {
        var document = XDocument.Load(SourcePath("TranslationToolView.xaml"));
        var menu = document.Descendants(Presentation + "ScrollViewer")
            .Single(element => (string?)element.Attribute(X + "Name") ==
                "TranslationHeaderMenuScrollViewer");

        Assert.Equal("{StaticResource FloatingToolsSharedScrollViewerStyle}",
            (string?)menu.Attribute("Style"));
        Assert.Equal("Auto", (string?)menu.Attribute("VerticalScrollBarVisibility"));
    }
    [Fact]
    public void TranslationMenu_UsesItsOwnBoundedScrollViewportOnlyInTopDock()
        => WpfTestApplication.Run(() =>
        {
            var view = new TranslationToolView();
            var menu = (Border)view.FindName("TranslationHeaderExpandedContent");
            var scroller = (ScrollViewer)view.FindName("TranslationHeaderMenuScrollViewer");
            var feedHost = (Grid)view.FindName("TranslationFeedHost");
            menu.Visibility = Visibility.Visible;
            view.SetTopDocked(true);
            var window = new Window
            {
                Content = view, Width = 460, Height = 190,
                Left = -10000, Top = -10000, ShowInTaskbar = false,
                ShowActivated = false, WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(
                    DispatcherPriority.ApplicationIdle, new Action(() => { }));
                view.UpdateLayout();

                Assert.True(view.IsTopDocked);
                Assert.Equal(feedHost.ActualHeight, menu.MaxHeight, 1);
                Assert.Equal(ScrollBarVisibility.Auto, scroller.VerticalScrollBarVisibility);
                Assert.True(scroller.ScrollableHeight > 0);
                Assert.Same(Application.Current.FindResource("FloatingToolsSharedScrollViewerStyle"),
                    scroller.Style);
                var scrollbar = (ScrollBar)scroller.Template.FindName(
                    "PART_VerticalScrollBar", scroller);
                Assert.Same(Application.Current.FindResource("FloatingToolsSharedMinimalScrollBarStyle"),
                    scrollbar.Style);
                Assert.True(scrollbar.IsVisible);
                scroller.ScrollToBottom();
                view.UpdateLayout();
                Assert.Equal(scroller.ScrollableHeight, scroller.VerticalOffset, 1);

                view.SetTopDocked(false);
                Assert.False(view.IsTopDocked);
                Assert.True(double.IsPositiveInfinity(menu.MaxHeight));
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public void TopOnlyReadingWidths_DoNotConstrainEditorsOrSideDockMessages()
    {
        var translation = XDocument.Load(SourcePath("TranslationToolView.xaml"));
        var quickChat = XDocument.Load(SourcePath("QuickChatToolView.xaml"));

        foreach (var name in new[] { "SavedWordItemSurface", "FrequentWordItemSurface" })
        {
            var item = translation.Descendants(Presentation + "Border")
                .Single(element => (string?)element.Attribute(X + "Name") == name);
            var template = item.Parent!;
            var trigger = template.Element(Presentation + "DataTemplate.Triggers")!
                .Elements(Presentation + "DataTrigger").Single();
            Assert.Equal("True", (string?)trigger.Attribute("Value"));
            Assert.Contains("IsTopDocked", (string?)trigger.Attribute("Binding"));
            Assert.Contains(trigger.Elements(Presentation + "Setter"), setter =>
                (string?)setter.Attribute("TargetName") == name
                && (string?)setter.Attribute("Value") ==
                    "{StaticResource TopDockCompactReadingWidthStyle}");
            Assert.Null(item.Attribute("MaxWidth"));
            Assert.Contains(item.Descendants(Presentation + "TextBox"),
                text => text.Attribute("FlowDirection") is not null
                    && text.Attribute("TextAlignment") is not null);
        }

        var chatTemplate = quickChat.Descendants(Presentation + "DataTemplate")
            .Single(template => template.Descendants(Presentation + "Border")
                .Any(element => (string?)element.Attribute(X + "Name") == "MessageSurface"));
        var surface = chatTemplate.Descendants(Presentation + "Border")
            .Single(element => (string?)element.Attribute(X + "Name") == "MessageSurface");
        var triggers = chatTemplate.Element(Presentation + "DataTemplate.Triggers")!
            .Elements(Presentation + "DataTrigger").ToArray();
        Assert.Contains("IsTopDocked", (string?)triggers[0].Attribute("Binding"));
        Assert.Contains(triggers[0].Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("TargetName") == "MessageSurface"
            && (string?)setter.Attribute("Property") == "MaxWidth"
            && (string?)setter.Attribute("Value") ==
                "{StaticResource TopDockDefaultReadingMaxWidth}");
        Assert.Equal("{Binding IsUser}", (string?)triggers[1].Attribute("Binding"));
        Assert.Contains(triggers[1].Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "HorizontalAlignment"
            && (string?)setter.Attribute("Value") == "Right");
        Assert.Equal("Stretch", (string?)surface.Attribute("HorizontalAlignment"));
        Assert.Null(surface.Attribute("MaxWidth"));
        Assert.Null(quickChat.Descendants(Presentation + "Border")
            .Single(element => (string?)element.Attribute(X + "Name") == "ComposerContainer")
            .Attribute("MaxWidth"));
    }

    private static string SourcePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(directory.FullName, "src", "FloatingTools.App", "Views", name);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate FloatingTools.sln.");
    }
}
