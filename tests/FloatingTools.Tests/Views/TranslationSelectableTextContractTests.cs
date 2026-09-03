using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class TranslationSelectableTextContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void SelectableEntryTextStyle_IsReadOnlyBorderlessAndDoesNotReserveScrollingChrome()
    {
        var view = XDocument.Load(FindSourcePath("Views", "TranslationToolView.xaml"));
        var style = view.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(Xaml + "Key")
                == "SelectableEntryTextStyle");
        var setters = style.Elements(Presentation + "Setter")
            .ToDictionary(
                element => (string)element.Attribute("Property")!,
                element => (string?)element.Attribute("Value"));

        Assert.Equal("True", setters["IsReadOnly"]);
        Assert.Equal("False", setters["IsReadOnlyCaretVisible"]);
        Assert.Equal("Wrap", setters["TextWrapping"]);
        Assert.Equal("True", setters["AcceptsReturn"]);
        Assert.Equal("Transparent", setters["Background"]);
        Assert.Equal("0", setters["BorderThickness"]);
        Assert.Equal("0", setters["Padding"]);
        Assert.Equal("Disabled", setters["VerticalScrollBarVisibility"]);
        Assert.Equal("Disabled", setters["HorizontalScrollBarVisibility"]);
        Assert.Equal("IBeam", setters["Cursor"]);
        Assert.Equal("{x:Null}", setters["FocusVisualStyle"]);
        Assert.Equal("False", setters["IsTabStop"]);
        Assert.Equal(
            "True",
            setters["sharedControls:TripleClickSelectAllBehavior.IsEnabled"]);
    }

    [Fact]
    public void FeedEntries_UseSelectableStyleWithOneWayTextAndResolvedDirection()
    {
        var view = XDocument.Load(FindSourcePath("Views", "TranslationToolView.xaml"));
        var feed = view.Descendants(Presentation + "ItemsControl")
            .Single(element => (string?)element.Attribute("ItemsSource") == "{Binding Items}");
        var selectable = feed.Descendants(Presentation + "TextBox")
            .Where(element => (string?)element.Attribute("Style")
                == "{StaticResource SelectableEntryTextStyle}")
            .ToArray();

        Assert.NotEmpty(selectable);
        Assert.All(selectable, element =>
        {
            var text = (string?)element.Attribute("Text");
            Assert.NotNull(text);
            Assert.Contains("Mode=OneWay", text);
        });

        var source = selectable.Single(element =>
            (string?)element.Attribute("Text") == "{Binding SourceText, Mode=OneWay}");
        Assert.Equal("{Binding SourceFlowDirection}", (string?)source.Attribute("FlowDirection"));
        Assert.Equal("{Binding SourceTextAlignment}", (string?)source.Attribute("TextAlignment"));

        var translation = selectable.Single(element =>
            (string?)element.Attribute("Text") == "{Binding MainTranslation, Mode=OneWay}");
        Assert.Equal("{Binding ResultFlowDirection}", (string?)translation.Attribute("FlowDirection"));
        Assert.Equal("{Binding ResultTextAlignment}", (string?)translation.Attribute("TextAlignment"));

        var alternativesList = feed.Descendants(Presentation + "ItemsControl")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding Alternatives}");
        var alternative = alternativesList.Descendants(Presentation + "TextBox")
            .Single(element => (string?)element.Attribute("Style")
                == "{StaticResource SelectableEntryTextStyle}");
        Assert.Equal("{Binding Text, Mode=OneWay}", (string?)alternative.Attribute("Text"));
        Assert.Equal("{Binding FlowDirection}", (string?)alternative.Attribute("FlowDirection"));
        Assert.Equal("{Binding TextAlignment}", (string?)alternative.Attribute("TextAlignment"));
    }

    [Fact]
    public void SavedAndFrequentWordEntries_AlsoUseTheSharedSelectableStyle()
    {
        var view = XDocument.Load(FindSourcePath("Views", "TranslationToolView.xaml"));
        var savedWordsGrid = FindPageGrid(view, "IsSavedWordsPage");
        var frequentWordsGrid = FindPageGrid(view, "IsFrequentWordsPage");

        foreach (var section in new[] { savedWordsGrid, frequentWordsGrid })
        {
            var boxes = section.Descendants(Presentation + "TextBox")
                .Where(element => (string?)element.Attribute("Style")
                    == "{StaticResource SelectableEntryTextStyle}")
                .ToArray();
            Assert.Equal(2, boxes.Length);
            Assert.All(boxes, element =>
                Assert.Contains("Mode=OneWay", (string?)element.Attribute("Text")));
        }
    }

    private static XElement FindPageGrid(XDocument view, string flagPropertyName) =>
        view.Descendants(Presentation + "Grid")
            .Single(element => element
                .Element(Presentation + "Grid.Style")
                ?.Descendants(Presentation + "DataTrigger")
                .Any(trigger => (string?)trigger.Attribute("Binding")
                    == $"{{Binding {flagPropertyName}}}") == true);

    [Fact]
    public void ExistingExplicitCopyCommandsRemainUnchanged()
    {
        var view = XDocument.Load(FindSourcePath("Views", "TranslationToolView.xaml"));
        var copyButtons = view.Descendants(Presentation + "Button")
            .Where(element => (string?)element.Attribute("Command") == "{Binding CopyCommand}")
            .ToArray();

        Assert.Equal(3, copyButtons.Length);
    }

    [Fact]
    public void NoCustomClickCountOrManualSelectionHandlerWasIntroduced()
    {
        var source = File.ReadAllText(FindSourcePath("Views", "TranslationToolView.xaml"));
        var codeBehind = File.ReadAllText(FindSourcePath("Views", "TranslationToolView.xaml.cs"));

        Assert.DoesNotContain("ClickCount", source);
        Assert.DoesNotContain("ClickCount", codeBehind);
        Assert.DoesNotContain("MouseMove", source);
        Assert.DoesNotContain("MouseMove", codeBehind);
    }

    private static string FindSourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(
                    [directory.FullName, "src", "FloatingTools.App", .. parts]);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
