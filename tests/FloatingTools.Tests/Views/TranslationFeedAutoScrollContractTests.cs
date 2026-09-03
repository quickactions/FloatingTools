using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Contract coverage for the entry auto-scroll polish: expanding a
/// Translation entry (or loading an alternative inside it) should ask WPF
/// to bring the entry into view via the existing feed ScrollViewer, using
/// native BringIntoView geometry rather than a manually-computed offset or
/// a second ScrollViewer. The actual pixel-perfect scroll behavior isn't
/// reliably observable in this headless test harness (no real layout/
/// render pass), so these checks are limited to the wiring that's safe to
/// assert statically: the behavior is attached to the right element, bound
/// to the right property, and no nested ScrollViewer was introduced.
/// </summary>
public sealed class TranslationFeedAutoScrollContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MainFeedEntryTemplate_BindsBringIntoViewOnRevealBehaviorToRevealRequestVersion()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        var feedScrollViewer = document.Descendants(Presentation + "ScrollViewer")
            .Single(element => (string?)element.Attribute(X + "Name") == "FeedScrollViewer");
        var entryBorder = feedScrollViewer
            .Descendants(Presentation + "DataTemplate")
            .First()
            .Elements(Presentation + "Border")
            .Single();

        var revealVersionAttribute = entryBorder.Attributes()
            .SingleOrDefault(attribute =>
                attribute.Name.LocalName == "BringIntoViewOnRevealBehavior.RevealVersion");

        Assert.NotNull(revealVersionAttribute);
        Assert.Equal("{Binding RevealRequestVersion}", revealVersionAttribute!.Value);
    }

    [Fact]
    public void MainFeedEntryTemplate_IntroducesNoNestedScrollViewer()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        var feedScrollViewer = document.Descendants(Presentation + "ScrollViewer")
            .Single(element => (string?)element.Attribute(X + "Name") == "FeedScrollViewer");
        var entryTemplate = feedScrollViewer.Descendants(Presentation + "DataTemplate").First();

        Assert.Empty(entryTemplate.Descendants(Presentation + "ScrollViewer"));
    }

    [Fact]
    public void SharedControlsNamespace_IsDeclaredForTheBehaviorToResolve()
    {
        var text = File.ReadAllText(FindTranslationToolViewPath());

        Assert.Contains(
            "xmlns:sharedControls=\"clr-namespace:FloatingTools.App.SharedUi.Controls\"",
            text);
    }

    private static string FindTranslationToolViewPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "FloatingTools.sln");
            if (File.Exists(solution))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "FloatingTools.App",
                    "Views",
                    "TranslationToolView.xaml");
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate TranslationToolView.xaml.");
    }
}
