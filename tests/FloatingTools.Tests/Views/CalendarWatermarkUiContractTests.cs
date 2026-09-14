using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Stage 1 contract: the Full Events search watermark lives inside the shared
/// input template rather than as an independently positioned sibling overlay.
/// </summary>
public sealed class CalendarWatermarkUiContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void FullEventsSearchTextBox_DeclaresWatermarkTextAndKeepsItsGutter()
    {
        var document = LoadView();
        var search = document.Descendants(Presentation + "TextBox")
            .Single(element => (string?)element.Attribute(X + "Name") == "FullEventsSearchTextBox");

        Assert.Equal(
            "{Binding EventsViewModel.SearchLabel}",
            (string?)search.Attribute(Controls + "TextBoxWatermark.Text"));
        Assert.Equal("7,0,30,0", (string?)search.Attribute("Padding"));
        Assert.Equal(
            "{Binding EventsViewModel.SearchText, UpdateSourceTrigger=PropertyChanged}",
            (string?)search.Attribute("Text"));
        Assert.Equal(
            "{StaticResource FullEventsFilledSearchStyle}",
            (string?)search.Attribute("Style"));
    }

    [Fact]
    public void SharedWatermark_CollapsesWhileTheFieldHasKeyboardFocus()
    {
        var document = XDocument.Load(FindSourcePath("SharedUi/Styles/Inputs.xaml"));
        var triggers = document.Descendants(Presentation + "ControlTemplate.Triggers").Single();
        var multi = triggers.Elements(Presentation + "MultiTrigger").Single();
        var conditions = multi.Descendants(Presentation + "Condition").ToArray();

        Assert.Contains(conditions, condition =>
            (string?)condition.Attribute("Property") == "Text"
            && (string?)condition.Attribute("Value") == string.Empty);
        Assert.Contains(conditions, condition =>
            (string?)condition.Attribute("Property") == "IsKeyboardFocused"
            && (string?)condition.Attribute("Value") == "True");
        Assert.Equal(
            "Collapsed",
            (string?)multi.Elements(Presentation + "Setter").Single().Attribute("Value"));

        // Trigger order decides the empty+focused case; the MultiTrigger must
        // come after the plain Text triggers that make the watermark visible.
        var children = triggers.Elements().ToList();
        var lastVisible = children.FindLastIndex(element =>
            element.Name == Presentation + "Trigger"
            && (string?)element.Attribute("Property") == "Text");
        Assert.True(children.IndexOf(multi) > lastVisible);
    }

    private static readonly XNamespace Controls =
        "clr-namespace:FloatingTools.App.Controls";

    private static XDocument LoadView() =>
        XDocument.Load(FindSourcePath("Views/CalendarToolView.xaml"));

    private static string FindSourcePath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "FloatingTools.App",
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
