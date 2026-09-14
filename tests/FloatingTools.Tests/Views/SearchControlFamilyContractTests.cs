using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Stage 1.5 contract: every Search field in the app shares one visual/input
/// foundation - FloatingToolsSearchTextBoxStyle or a thin BasedOn alias of it -
/// with the watermark supplied by the shared template rather than by a
/// hand-positioned sibling TextBlock.
/// </summary>
public sealed class SearchControlFamilyContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Controls =
        "clr-namespace:FloatingTools.App.Controls";

    // view file, element name, style key applied at the call site
    public static TheoryData<string, string, string> SearchFields => new()
    {
        { "Views/CalendarToolView.xaml", "FullEventsSearchTextBox", "FullEventsFilledSearchStyle" },
        { "Views/CalendarToolView.xaml", "CalendarSearchTextBox", "CalendarMenuSearchStyle" },
        { "Views/NotesToolView.xaml", "NotesSearchTextBox", "NotesSearchTextBoxStyle" },
        { "Views/TranslationToolView.xaml", "SavedWordsSearchTextBox", "SavedWordsSearchTextBoxStyle" },
    };

    [Theory]
    [MemberData(nameof(SearchFields))]
    public void EverySearchField_UsesTheSharedSearchStyleOrAThinAliasOfIt(
        string viewPath, string elementName, string styleKey)
    {
        var document = XDocument.Load(FindSourcePath(viewPath));
        var field = FindNamed(document, elementName);

        Assert.Equal($"{{StaticResource {styleKey}}}", (string?)field.Attribute("Style"));

        var style = document.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(X + "Key") == styleKey);
        Assert.Equal(
            "{StaticResource FloatingToolsSearchTextBoxStyle}",
            (string?)style.Attribute("BasedOn"));

        // A thin alias may retint, but must never re-declare the template:
        // one template is the whole point of the family.
        Assert.DoesNotContain(
            style.Descendants(Presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "Template");
    }

    [Theory]
    [MemberData(nameof(SearchFields))]
    public void EverySearchField_DeclaresItsWatermarkOnTheControl(
        string viewPath, string elementName, string styleKey)
    {
        _ = styleKey;
        var document = XDocument.Load(FindSourcePath(viewPath));
        var field = FindNamed(document, elementName);

        Assert.False(
            string.IsNullOrWhiteSpace(
                (string?)field.Attribute(Controls + "TextBoxWatermark.Text")),
            $"{elementName} should carry its placeholder as TextBoxWatermark.Text.");
    }

    [Theory]
    [MemberData(nameof(SearchFields))]
    public void EverySearchField_HasNoSiblingHandPositionedPlaceholder(
        string viewPath, string elementName, string styleKey)
    {
        _ = styleKey;
        var document = XDocument.Load(FindSourcePath(viewPath));
        var field = FindNamed(document, elementName);
        var host = field.Parent!;

        // A sibling TextBlock positioned by its own margin is exactly the
        // inset drift this stage removes.
        Assert.DoesNotContain(
            host.Elements(Presentation + "TextBlock"),
            text => text.Attribute("Margin") is not null);
    }

    [Fact]
    public void SharedTemplate_KeepsOneSourceOfTruthForTheEditableInset()
    {
        var document = XDocument.Load(FindSourcePath("SharedUi/Styles/Inputs.xaml"));
        var watermark = document.Descendants(Presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(X + "Name") == "Watermark");
        var contentHost = document.Descendants(Presentation + "ScrollViewer")
            .Single(element => (string?)element.Attribute(X + "Name") == "PART_ContentHost");

        Assert.Equal("{TemplateBinding Padding}", (string?)watermark.Attribute("Margin"));
        Assert.Null(contentHost.Attribute("Margin"));
    }

    [Fact]
    public void MigrationDidNotLeakIntoTheControlsThatShareTheOldStyles()
    {
        var calendar = XDocument.Load(FindSourcePath("Views/CalendarToolView.xaml"));
        var notes = XDocument.Load(FindSourcePath("Views/NotesToolView.xaml"));

        // The event editor still owns CalendarSingleLineInputStyle...
        Assert.Equal(
            "{StaticResource CalendarSingleLineInputStyle}",
            (string?)FindNamed(calendar, "EventEditorTextBox").Attribute("Style"));

        // ...and the rename box still owns NotesInputTextBoxStyle, so neither
        // style could be edited to serve the search migration.
        Assert.Contains(
            notes.Descendants(Presentation + "Style"),
            style => (string?)style.Attribute("BasedOn") == "{StaticResource NotesInputTextBoxStyle}");
    }

    private static XElement FindNamed(XDocument document, string name) =>
        document.Descendants()
            .Single(element => (string?)element.Attribute(X + "Name") == name);

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
