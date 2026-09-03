using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class ToolHeaderSharedStyleContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void SharedToolHeaderStyle_ProvidesTheCommonTitleAndChevronChrome()
    {
        var styleDocument = LoadXaml("SharedUi", "Styles", "ToolHeader.xaml");
        var style = FindStyle(styleDocument, "FloatingToolsSharedToolHeaderStyle");

        Assert.Equal(
            "{StaticResource FloatingToolsSharedButtonBaseStyle}",
            (string?)style.Attribute("BasedOn"));
        Assert.Equal(
            "{x:Type controls:ToolHeaderControl}",
            (string?)style.Attribute("TargetType"));
        Assert.Contains(
            style.Descendants(Presentation + "TextBlock"),
            textBlock => (string?)textBlock.Attribute("Text") == "{TemplateBinding Title}");
        Assert.Contains(
            style.Descendants(Presentation + "ContentPresenter"),
            presenter => (string?)presenter.Attribute("Content")
                == "{TemplateBinding IsMenuOpen}");
        Assert.Contains(
            "SharedUi/Styles/ToolHeader.xaml",
            File.ReadAllText(Path.Combine(FindSolutionRoot(), "src", "FloatingTools.App", "App.xaml")));
    }

    [Fact]
    public void TranslationAndNotesHeaders_UseTheSharedControlWithFeatureOwnedState()
    {
        var translation = LoadXaml("Views", "TranslationToolView.xaml");
        var notes = LoadXaml("Views", "NotesToolView.xaml");
        var translationHeader = FindToolHeader(translation);
        var notesHeader = FindToolHeader(notes);

        Assert.Equal("Translation Assistant", (string?)translationHeader.Attribute("Title"));
        Assert.Equal("{Binding IsAppMenuOpen}", (string?)translationHeader.Attribute("IsMenuOpen"));
        Assert.Equal(
            "{Binding ElementName=TranslationHeaderExpandedContent}",
            (string?)translationHeader.Attribute("ExpandedContentRoot"));
        Assert.Equal(
            "{Binding ToggleAppMenuCommand}",
            (string?)translationHeader.Attribute("Command"));
        Assert.Equal(
            "{StaticResource TranslationToolHeaderStyle}",
            (string?)translationHeader.Attribute("Style"));

        Assert.Equal("{Binding ActiveTitle}", (string?)notesHeader.Attribute("Title"));
        Assert.Equal("{Binding IsMenuOpen}", (string?)notesHeader.Attribute("IsMenuOpen"));
        Assert.Equal(
            "{Binding ElementName=NotesHeaderExpandedContent}",
            (string?)notesHeader.Attribute("ExpandedContentRoot"));
        Assert.Equal(
            "{Binding ToggleMenuCommand}",
            (string?)notesHeader.Attribute("Command"));
        Assert.Equal(
            "{StaticResource NotesToolHeaderStyle}",
            (string?)notesHeader.Attribute("Style"));
        Assert.DoesNotContain(
            translationHeader.Attributes(),
            attribute => attribute.Name.LocalName.StartsWith(
                "SecondaryAction", StringComparison.Ordinal));
        Assert.DoesNotContain(
            notesHeader.Attributes(),
            attribute => attribute.Name.LocalName.StartsWith(
                "SecondaryAction", StringComparison.Ordinal));
        Assert.Null(translationHeader.Attribute("IsSecondaryActionVisible"));
        Assert.Null(notesHeader.Attribute("IsSecondaryActionVisible"));

        Assert.Equal(
            "{StaticResource FloatingToolsSharedToolHeaderStyle}",
            (string?)FindStyle(translation, "TranslationToolHeaderStyle").Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedToolHeaderStyle}",
            (string?)FindStyle(notes, "NotesToolHeaderStyle").Attribute("BasedOn"));
        Assert.Contains(
            notes.Descendants(Presentation + "Border"),
            border => (string?)border.Attribute(X + "Name") == "NotesHeaderExpandedContent");
        Assert.Contains(
            translation.Descendants(Presentation + "Border"),
            border => (string?)border.Attribute(X + "Name") == "TranslationHeaderExpandedContent");
    }

    [Fact]
    public void OptionalSecondaryAction_UsesOverlayLayoutAndCollapsesWhileExpanded()
    {
        var document = LoadXaml("SharedUi", "Styles", "ToolHeader.xaml");
        var style = FindStyle(document, "FloatingToolsSharedToolHeaderStyle");
        var action = style.Descendants(Presentation + "Button")
            .Single(button => (string?)button.Attribute(X + "Name")
                == "PART_SecondaryAction");
        var localStyle = action.Element(Presentation + "Button.Style")!
            .Element(Presentation + "Style")!;
        var templateGrid = action.Parent!;

        Assert.Equal("Left", (string?)action.Attribute("HorizontalAlignment"));
        Assert.Equal(
            "{Binding SecondaryActionCommand, RelativeSource={RelativeSource TemplatedParent}}",
            (string?)action.Attribute("Command"));
        Assert.Equal(
            "{TemplateBinding SecondaryActionContent}",
            (string?)action.Attribute("Content"));
        Assert.Equal(
            "{TemplateBinding SecondaryActionToolTip}",
            (string?)action.Attribute("ToolTip"));
        Assert.Equal(
            "{TemplateBinding SecondaryActionAutomationName}",
            (string?)action.Attribute("AutomationProperties.Name"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedIconButtonStyle}",
            (string?)localStyle.Attribute("BasedOn"));
        Assert.Contains(localStyle.Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Visibility"
            && (string?)setter.Attribute("Value") == "Collapsed");

        var conditions = localStyle.Descendants(Presentation + "Condition").ToArray();
        Assert.Contains(conditions, condition =>
            ((string?)condition.Attribute("Binding"))
                ?.Contains("IsSecondaryActionVisible") == true
            && (string?)condition.Attribute("Value") == "True");
        Assert.Contains(conditions, condition =>
            ((string?)condition.Attribute("Binding"))?.Contains("IsMenuOpen") == true
            && (string?)condition.Attribute("Value") == "False");
        Assert.Null(templateGrid.Element(Presentation + "Grid.ColumnDefinitions"));
    }

    [Fact]
    public void TranslationDockSidePlumbing_IsAbsentAndScrollbarsRemainRightSideOnly()
    {
        var root = FindSolutionRoot();
        var translationXaml = File.ReadAllText(Path.Combine(
            root, "src", "FloatingTools.App", "Views", "TranslationToolView.xaml"));
        var translationCodeBehind = File.ReadAllText(Path.Combine(
            root, "src", "FloatingTools.App", "Views", "TranslationToolView.xaml.cs"));
        var panelCodeBehind = File.ReadAllText(Path.Combine(
            root, "src", "FloatingTools.App", "Views", "PanelWindow.xaml.cs"));
        var scrollBars = File.ReadAllText(Path.Combine(
            root, "src", "FloatingTools.App", "SharedUi", "Styles", "ScrollBars.xaml"));

        Assert.DoesNotContain("DockSide", translationXaml);
        Assert.DoesNotContain("DockSide", translationCodeBehind);
        Assert.DoesNotContain("TranslationTool.DockSide", panelCodeBehind);
        Assert.Contains(
            "BasedOn=\"{StaticResource FloatingToolsSharedScrollViewerStyle}\"",
            translationXaml);
        Assert.Contains("HorizontalAlignment=\"Right\"", scrollBars);
    }

    private static XElement FindStyle(XDocument document, string key) =>
        document.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(X + "Key") == key);

    private static XElement FindToolHeader(XDocument document) =>
        document.Descendants()
            .Single(element => element.Name.LocalName == "ToolHeaderControl");

    private static XDocument LoadXaml(params string[] pathSegments) =>
        XDocument.Load(Path.Combine(
            [FindSolutionRoot(), "src", "FloatingTools.App", .. pathSegments]));

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
