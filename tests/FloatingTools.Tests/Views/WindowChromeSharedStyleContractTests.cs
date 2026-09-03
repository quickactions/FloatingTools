using System.Xml.Linq;
using FloatingTools.App.Models;

namespace FloatingTools.Tests.Views;

public sealed class WindowChromeSharedStyleContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ToolbarChrome_UsesSharedBasesAndPreservesItsSpecializedInteractions()
    {
        var document = LoadView("ToolbarWindow.xaml");
        var contextMenuStyle = FindImplicitStyle(
            document, "{x:Type ContextMenu}");
        var menuItemStyle = FindImplicitStyle(document, "{x:Type MenuItem}");
        var toolbarButtonStyle = FindStyle(document, "ToolbarButtonStyle");
        var overflowButtonStyle = FindStyle(document, "OverflowButtonStyle");

        Assert.Equal(
            "{StaticResource FloatingToolsSharedContextMenuStyle}",
            (string?)contextMenuStyle.Attribute("BasedOn"));
        Assert.NotEmpty(contextMenuStyle.Descendants(Presentation + "DropShadowEffect"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedMenuItemStyle}",
            (string?)menuItemStyle.Attribute("BasedOn"));
        Assert.Contains(
            "{DynamicResource FloatingToolsBrushSurfacePressed}",
            menuItemStyle.ToString());
        Assert.Contains(
            "{DynamicResource FloatingToolsBrushSurfaceSubtleHover}",
            menuItemStyle.ToString());
        Assert.Equal(
            "{StaticResource FloatingToolsSharedButtonBaseStyle}",
            (string?)toolbarButtonStyle.Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedButtonBaseStyle}",
            (string?)overflowButtonStyle.Attribute("BasedOn"));
        Assert.Equal(
            2,
            document.Descendants(Presentation + "TranslateTransform")
                .Count(transform => (string?)transform.Attribute("Y") == "1"));
    }

    [Fact]
    public void ToolbarAndPanelToolTips_UseTheSharedToolTipStyle()
    {
        foreach (var view in new[] { "ToolbarWindow.xaml", "PanelWindow.xaml" })
        {
            var style = FindImplicitStyle(LoadView(view), "{x:Type ToolTip}");

            Assert.Equal(
                "{StaticResource FloatingToolsSharedToolTipStyle}",
                (string?)style.Attribute("BasedOn"));
        }
    }

    [Fact]
    public void PanelButtonsAndSizeMenu_UseTheSharedChromeStyles()
    {
        var document = LoadView("PanelWindow.xaml");
        var buttons = document.Descendants(Presentation + "Button").ToArray();
        var sizeMenu = document.Descendants(Presentation + "ContextMenu").Single();
        var sizeMenuItemStyle = FindStyle(document, "PanelSizeMenuItemStyle");

        Assert.Equal(Enum.GetValues<ToolId>().Length + 3, buttons.Length);

        var tileButtons = buttons
            .Where(button => (string?)button.Attribute("Command") == "{Binding SelectToolCommand}")
            .ToArray();
        var chromeButtons = buttons.Except(tileButtons).ToArray();

        Assert.Equal(Enum.GetValues<ToolId>().Length, tileButtons.Length);
        Assert.Equal(3, chromeButtons.Length);
        Assert.All(
            tileButtons,
            button => Assert.Equal(
                "{StaticResource FloatingToolsSharedToolTileButtonStyle}",
                (string?)button.Attribute("Style")));
        Assert.All(
            chromeButtons,
            button => Assert.Equal(
                "{StaticResource FloatingToolsSharedChromeButtonStyle}",
                (string?)button.Attribute("Style")));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedContextMenuStyle}",
            (string?)sizeMenu.Attribute("Style"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedMenuItemStyle}",
            (string?)sizeMenuItemStyle.Attribute("BasedOn"));
    }

    [Fact]
    public void ToolTileButtonStyle_IsBasedOnSharedBaseAndUsesTheControlCornerRadius()
    {
        var document = XDocument.Load(Path.Combine(
            FindSolutionRoot(), "src", "FloatingTools.App", "SharedUi", "Styles", "Buttons.xaml"));
        var style = FindStyle(document, "FloatingToolsSharedToolTileButtonStyle");
        var surface = style.Descendants(Presentation + "Border")
            .Single(border => (string?)border.Attribute(X + "Name") == "ButtonSurface");

        Assert.Equal(
            "{StaticResource FloatingToolsSharedButtonBaseStyle}",
            (string?)style.Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsRadiusControl}",
            (string?)surface.Attribute("CornerRadius"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceSubtleHover}",
            style.Descendants(Presentation + "Trigger")
                .Single(trigger => (string?)trigger.Attribute("Property") == "IsMouseOver")
                .Elements(Presentation + "Setter")
                .Single()
                .Attribute("Value")?.Value);
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfacePressed}",
            style.Descendants(Presentation + "Trigger")
                .Single(trigger => (string?)trigger.Attribute("Property") == "IsPressed")
                .Elements(Presentation + "Setter")
                .Single()
                .Attribute("Value")?.Value);
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceFocus}",
            style.Descendants(Presentation + "Trigger")
                .Single(trigger => (string?)trigger.Attribute("Property") == "IsKeyboardFocused")
                .Elements(Presentation + "Setter")
                .Single()
                .Attribute("Value")?.Value);
    }

    [Fact]
    public void ToolTiles_UseTheFixedFortyFourSizeWithATwoPixelMargin()
    {
        var document = LoadView("PanelWindow.xaml");
        var tileButtons = document.Descendants(Presentation + "Button")
            .Where(button => (string?)button.Attribute("Command") == "{Binding SelectToolCommand}")
            .ToArray();

        Assert.Equal(Enum.GetValues<ToolId>().Length, tileButtons.Length);
        Assert.All(tileButtons, button =>
        {
            Assert.Equal("44", (string?)button.Attribute("Width"));
            Assert.Equal("44", (string?)button.Attribute("Height"));
            Assert.Equal("2", (string?)button.Attribute("Margin"));
        });
    }

    [Fact]
    public void PanelSizeMenu_PreservesItsCheckmarkAndWideRows()
    {
        var document = LoadView("PanelWindow.xaml");
        var style = FindStyle(document, "PanelSizeMenuItemStyle");

        Assert.Equal("126", GetSetterValue(style, "MinWidth"));
        Assert.Equal("8,0", GetSetterValue(style, "Padding"));
        Assert.Contains(
            style.Descendants(Presentation + "TextBlock"),
            element => (string?)element.Attribute(X + "Name") == "CheckMark"
                && (string?)element.Attribute("Text") == "✓");
        Assert.Contains(
            style.Descendants(Presentation + "Trigger"),
            trigger => (string?)trigger.Attribute("Property") == "IsChecked"
                && (string?)trigger.Attribute("Value") == "True");
    }

    [Fact]
    public void PanelChrome_PreservesItsLocalHeaderSurfaceAndSharedBaseTokens()
    {
        var document = LoadView("PanelWindow.xaml");
        var panels = document.Descendants(Presentation + "Border")
            .Where(border =>
                (string?)border.Attribute(X + "Name") is "ToolMenuPanel" or "ActiveToolPanel")
            .ToArray();
        var headers = document.Descendants(Presentation + "Border")
            .Where(border =>
                (string?)border.Attribute(X + "Name") is "ToolMenuHeader" or "ActiveToolHeader")
            .ToArray();

        Assert.Equal(2, panels.Length);
        Assert.Equal(2, headers.Length);
        Assert.All(
            panels,
            panel => Assert.Equal(
                "{DynamicResource FloatingToolsBrushSurfaceInput}",
                (string?)panel.Attribute("Background")));
        Assert.All(headers, header =>
        {
            Assert.Equal(
                "{DynamicResource FloatingToolsBrushSurfaceHeader}",
                (string?)header.Attribute("Background"));
            Assert.Equal(
                "{DynamicResource FloatingToolsBrushBorderDivider}",
                (string?)header.Attribute("BorderBrush"));
        });
    }

    [Fact]
    public void PanelActiveContent_UsesTheDockDerivedCornerClip()
    {
        var xaml = File.ReadAllText(FindViewPath("PanelWindow.xaml"));
        var code = File.ReadAllText(FindViewPath("PanelWindow.xaml.cs"));

        Assert.Contains("x:Name=\"ActiveToolContent\"", xaml);
        Assert.Contains("SizeChanged=\"ActiveToolContent_OnSizeChanged\"", xaml);
        Assert.Contains("PanelChromeCornerRadiusCalculator.Calculate(dockSide)", code);
        Assert.Contains("ActiveToolContent.Clip = geometry", code);
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

    private static XDocument LoadView(string fileName) =>
        XDocument.Load(FindViewPath(fileName));

    private static string FindViewPath(string fileName) =>
        Path.Combine(FindSolutionRoot(), "src", "FloatingTools.App", "Views", fileName);

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
