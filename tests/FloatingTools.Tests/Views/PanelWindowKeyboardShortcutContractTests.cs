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
        var keyBindings = document.Descendants(Presentation + "KeyBinding").ToArray();

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
