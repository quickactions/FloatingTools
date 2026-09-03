using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class NotesLinkListSharedStyleContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void NotesChrome_UsesSharedBasesWhileRetainingItsApprovedSpecializations()
    {
        var document = LoadXaml("Views", "NotesToolView.xaml");

        Assert.Equal(
            "{StaticResource FloatingToolsSharedButtonBaseStyle}",
            (string?)FindStyle(document, "NotesTextActionStyle").Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedTextBoxStyle}",
            (string?)FindStyle(document, "NotesInputTextBoxStyle").Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedContextMenuStyle}",
            (string?)FindStyle(document, "NotesContextMenuStyle").Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedMenuItemStyle}",
            (string?)FindStyle(document, "NotesActionMenuItemStyle").Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedToolTipStyle}",
            (string?)FindImplicitStyle(document, "{x:Type ToolTip}").Attribute("BasedOn"));

        var noteMenuSurface = FindStyle(document, "NotesMenuSurfaceStyle");
        Assert.Equal(
            "{StaticResource FloatingToolsRadiusMenu}",
            GetSetterValue(noteMenuSurface, "CornerRadius"));
        Assert.Equal(
            "{StaticResource FloatingToolsSpacingMenuSurface}",
            GetSetterValue(noteMenuSurface, "Padding"));
    }

    [Fact]
    public void NotesScrollbars_UseTheEquivalentSharedUiStyle()
    {
        var document = LoadXaml("Views", "NotesToolView.xaml");

        Assert.Equal(
            2,
            document.Descendants(Presentation + "ScrollViewer")
                .Count(viewer => (string?)viewer.Attribute("Style")
                    == "{StaticResource FloatingToolsSharedScrollViewerStyle}"));
    }

    [Fact]
    public void LinkListChrome_UsesSharedButtonAndToolTipBasesWithoutChangingInputs()
    {
        var document = LoadXaml("Controls", "LinkListBlockControl.xaml");
        var toolbarButton = FindStyle(document, "LinkToolbarIconButton");
        var editInput = FindStyle(document, "LinkEditInput");
        var draftInput = document.Descendants(Presentation + "TextBox")
            .Single(input => (string?)input.Attribute(X + "Name") == "DraftToken");

        Assert.Equal(
            "{StaticResource FloatingToolsSharedButtonBaseStyle}",
            (string?)toolbarButton.Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedToolTipStyle}",
            (string?)FindImplicitStyle(document, "ToolTip").Attribute("BasedOn"));
        Assert.Equal("27", GetSetterValue(toolbarButton, "Width"));
        Assert.Equal("27", GetSetterValue(toolbarButton, "Height"));
        Assert.Equal("28", GetSetterValue(editInput, "Height"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceInput}",
            GetSetterValue(editInput, "Background"));
        Assert.Equal("White", (string?)draftInput.Attribute("Background"));
        Assert.Equal("0", (string?)draftInput.Attribute("BorderThickness"));
    }

    private static XElement FindImplicitStyle(XDocument document, string targetType) =>
        document.Descendants(Presentation + "Style")
            .Single(style =>
                (string?)style.Attribute("TargetType") == targetType
                && style.Attribute(X + "Key") is null);

    private static XElement FindStyle(XDocument document, string key) =>
        document.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(X + "Key") == key);

    private static string? GetSetterValue(XElement style, string property) =>
        (string?)style.Elements(Presentation + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == property)
            .Attribute("Value");

    private static XDocument LoadXaml(string directory, string fileName) =>
        XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "FloatingTools.App",
            directory,
            fileName));

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
