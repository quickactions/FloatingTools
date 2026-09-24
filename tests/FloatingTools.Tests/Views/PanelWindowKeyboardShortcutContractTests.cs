using System.Xml.Linq;
using FloatingTools.App.Models;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

public sealed class PanelWindowKeyboardShortcutContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void ToolSwitchKeyBindings_AreExactlyFourReusingSelectToolCommand()
    {
        var document = LoadView();
        var keyBindings = document.Descendants(Presentation + "KeyBinding")
            .Where(binding =>
                (string?)binding.Attribute("Command") == "{Binding SelectToolCommand}")
            .ToArray();

        Assert.Equal(4, keyBindings.Length);
        Assert.All(keyBindings, binding =>
        {
            Assert.Equal("Control", (string?)binding.Attribute("Modifiers"));
            Assert.Equal(
                "{Binding SelectToolCommand}",
                (string?)binding.Attribute("Command"));
        });

        AssertBinding(keyBindings, "D1", "{x:Static models:ToolId.Translation}");
        AssertBinding(keyBindings, "D2", "{x:Static models:ToolId.Notes}");
        AssertBinding(keyBindings, "D3", "{x:Static models:ToolId.QuickChat}");
        AssertBinding(keyBindings, "D4", "{x:Static models:ToolId.Calendar}");
    }

    [Fact]
    public void ZoomKeyBindings_CoverOrdinaryAndNumericKeypadInputs()
    {
        var document = LoadView();
        var bindings = document.Descendants(Presentation + "KeyBinding")
            .Where(binding => ((string?)binding.Attribute("Command"))?.Contains(
                "Zoom",
                StringComparison.Ordinal) == true)
            .Select(binding => (
                Key: (string?)binding.Attribute("Key"),
                Modifiers: (string?)binding.Attribute("Modifiers"),
                Command: (string?)binding.Attribute("Command")))
            .ToArray();

        Assert.Contains(("OemPlus", "Control", "{Binding ZoomInCommand}"), bindings);
        Assert.Contains(("OemPlus", "Control+Shift", "{Binding ZoomInCommand}"), bindings);
        Assert.Contains(("Add", "Control", "{Binding ZoomInCommand}"), bindings);
        Assert.Contains(("OemMinus", "Control", "{Binding ZoomOutCommand}"), bindings);
        Assert.Contains(("Subtract", "Control", "{Binding ZoomOutCommand}"), bindings);
        Assert.Contains(("D0", "Control", "{Binding ResetZoomCommand}"), bindings);
        Assert.Contains(("NumPad0", "Control", "{Binding ResetZoomCommand}"), bindings);
    }

    [Fact]
    public void Zoom_UsesKeyboardOnlyAndPreservesTransparentHitTesting()
    {
        var document = LoadView();
        var window = document.Root!;
        var nameAttribute = XName.Get(
            "Name",
            "http://schemas.microsoft.com/winfx/2006/xaml");
        var hostRoot = document.Descendants()
            .Single(element => (string?)element.Attribute(nameAttribute)
                == "PanelHostRoot");
        var visiblePanel = document.Descendants()
            .Single(element => (string?)element.Attribute(nameAttribute)
                == "VisiblePanelHost");
        var content = document.Descendants()
            .Single(element => (string?)element.Attribute(nameAttribute)
                == "ActiveToolContent");
        var code = File.ReadAllText(
            FindSourcePath("Views", "PanelWindow.xaml.cs"));

        Assert.Null((string?)window.Attribute("PreviewMouseWheel"));
        Assert.Null((string?)hostRoot.Attribute("Background"));
        Assert.Same(hostRoot, visiblePanel.Parent);
        Assert.Single(content.Descendants(Presentation + "ScaleTransform"));
        Assert.Empty(content.Ancestors(Presentation + "ScrollViewer"));
        Assert.Contains("WmNcHitTest", code);
        Assert.Contains("HtTransparent", code);
        Assert.Contains("WindowMessageHook", code);
        Assert.DoesNotContain("WmMouseWheel", code);
    }

    [Fact]
    public void FindCommandBinding_IsExactlyOneUsingApplicationCommandsFind()
    {
        var document = LoadView();
        var commandBindings = document.Descendants(Presentation + "CommandBinding").ToArray();

        var binding = Assert.Single(commandBindings);
        Assert.Equal("ApplicationCommands.Find", (string?)binding.Attribute("Command"));
        Assert.Equal("FindCommand_OnCanExecute", (string?)binding.Attribute("CanExecute"));
        Assert.Equal("FindCommand_OnExecuted", (string?)binding.Attribute("Executed"));
    }

    [Theory]
    [InlineData(ToolId.Translation, true)]
    [InlineData(ToolId.Notes, true)]
    [InlineData(ToolId.Calendar, true)]
    [InlineData(ToolId.QuickChat, false)]
    public void ToolSupportsSearch_MatchesTheApprovedSearchAudit(ToolId tool, bool expected)
    {
        Assert.Equal(expected, PanelWindow.ToolSupportsSearch(tool));
    }

    private static void AssertBinding(XElement[] keyBindings, string key, string parameter)
    {
        var binding = Assert.Single(
            keyBindings,
            element => (string?)element.Attribute("Key") == key);
        Assert.Equal(parameter, (string?)binding.Attribute("CommandParameter"));
    }

    private static XDocument LoadView() =>
        XDocument.Load(FindSourcePath("Views", "PanelWindow.xaml"));

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
