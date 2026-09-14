using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using FloatingTools.App.SharedUi.Controls;

namespace FloatingTools.Tests.Views;

public sealed class SettingsPageShellContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void SharedScrollContentGutter_HasOneNamedTwelvePixelDefinition()
    {
        var spacing = XDocument.Load(
            FindSourcePath("SharedUi", "Tokens", "Spacing.xaml"));
        var gutter = spacing.Descendants(Presentation + "Thickness")
            .Single(element => (string?)element.Attribute(X + "Key")
                == "FloatingToolsSpacingScrollContentGutter");

        Assert.Equal("0,0,12,0", gutter.Value);

        var shellStyle = XDocument.Load(
            FindSourcePath("SharedUi", "Styles", "SettingsPageShell.xaml"));
        var bodyScroller = Named(shellStyle, "ScrollViewer", "BodyScrollViewer");
        Assert.Equal(
            "{StaticResource FloatingToolsSpacingScrollContentGutter}",
            (string?)bodyScroller.Attribute("Padding"));
        Assert.Equal("Auto", (string?)bodyScroller.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)bodyScroller.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("True", AttributeValue(bodyScroller, "SmoothWheelScrollBehavior.IsEnabled"));
    }

    [Fact]
    public void ScrollableToolContent_UsesTheSharedGutterOnlyForScrollbarClearance()
    {
        var quickChat = XDocument.Load(
            FindSourcePath("Views", "QuickChatToolView.xaml"));
        AssertUsesGutter(Named(quickChat, "ScrollViewer", "ConversationScrollViewer"));

        var calendar = XDocument.Load(
            FindSourcePath("Views", "CalendarToolView.xaml"));
        foreach (var name in new[]
        {
            "MonthScrollViewer",
            "WeekScrollViewer",
            "DayPanelScrollViewer",
            "SearchResultsScrollViewer"
        })
        {
            AssertUsesGutter(Named(calendar, "ScrollViewer", name));
        }

        var translation = XDocument.Load(
            FindSourcePath("Views", "TranslationToolView.xaml"));
        AssertUsesGutter(Named(translation, "ScrollViewer", "FeedScrollViewer"));
        AssertTranslationListGutter(translation, "{Binding SavedWords.Items}", "12,2,0,4");
        AssertTranslationListGutter(translation, "{Binding FrequentWords.Items}", "12,2,0,4");
    }

    [Fact]
    public void AllSettingsPages_UseTheSharedShellWithoutNestedOuterScrollViewers()
    {
        AssertSettingsShell(
            "TranslationToolView.xaml",
            "{Binding BackToFeedCommand}",
            "Translation AI");
        AssertSettingsShell(
            "QuickChatToolView.xaml",
            "{Binding BackToChatCommand}",
            "Additional instructions");
        var calendarShell = AssertSettingsShell(
            "CalendarToolView.xaml",
            "{Binding BackToCalendarCommand}",
            "{Binding LanguageLabel}");
        Assert.Equal("{Binding ContentFlowDirection}",
            (string?)calendarShell.Attribute("TitleFlowDirection"));
        Assert.Equal("{Binding ContentTextAlignment}",
            (string?)calendarShell.Attribute("TitleTextAlignment"));

        var application = XDocument.Load(
            FindSourcePath("Views", "ApplicationSettingsView.xaml"));
        var applicationShell = application.Descendants()
            .Single(element => element.Name.LocalName == "SettingsPageShell");
        Assert.Equal("False", (string?)applicationShell.Attribute("IsHeaderVisible"));
        Assert.Equal("False", (string?)applicationShell.Attribute("IsBackVisible"));
        Assert.Empty(applicationShell.Descendants(Presentation + "ScrollViewer"));
        Assert.Contains(applicationShell.Descendants(Presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Application AI");
    }

    [Fact]
    public void OptionalBackButton_RemovesItsColumnAndHeaderCanCollapseCompletely()
    {
        var style = XDocument.Load(
            FindSourcePath("SharedUi", "Styles", "SettingsPageShell.xaml"));
        var triggers = style.Descendants(Presentation + "ControlTemplate.Triggers")
            .Single();
        var backTrigger = triggers.Elements(Presentation + "Trigger")
            .Single(trigger => (string?)trigger.Attribute("Property") == "IsBackVisible");
        var headerTrigger = triggers.Elements(Presentation + "Trigger")
            .Single(trigger => (string?)trigger.Attribute("Property") == "IsHeaderVisible");

        Assert.Contains(backTrigger.Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("TargetName") == "BackButton"
            && (string?)setter.Attribute("Property") == "Visibility"
            && (string?)setter.Attribute("Value") == "Collapsed");
        Assert.Contains(backTrigger.Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("TargetName") == "BackColumn"
            && (string?)setter.Attribute("Property") == "Width"
            && (string?)setter.Attribute("Value") == "0");
        Assert.Contains(headerTrigger.Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("TargetName") == "HeaderRow"
            && (string?)setter.Attribute("Property") == "Visibility"
            && (string?)setter.Attribute("Value") == "Collapsed");
    }

    [Fact]
    public void ShellDefaults_MatchApprovedSettingsChromeAndBackIsTrulyOptional()
        => WpfTestApplication.Run(() =>
        {
            var shell = new SettingsPageShell { Content = new TextBlock() };
            Assert.Equal(new Thickness(14, 4, 0, 12), shell.BodyMargin);
            Assert.True(shell.IsHeaderVisible);
            Assert.True(shell.IsBackVisible);
            Assert.Equal(FlowDirection.LeftToRight, shell.TitleFlowDirection);
            Assert.Equal(TextAlignment.Left, shell.TitleTextAlignment);
            Assert.Equal(FlowDirection.LeftToRight, shell.HeaderFlowDirection);

            shell.IsBackVisible = false;
            shell.IsHeaderVisible = false;
            Assert.True(shell.ApplyTemplate());
            var back = (Button)shell.Template.FindName("BackButton", shell);
            var header = (Grid)shell.Template.FindName("HeaderRow", shell);
            Assert.Equal(Visibility.Collapsed, back.Visibility);
            Assert.Equal(Visibility.Collapsed, header.Visibility);
        });

    private static XElement AssertSettingsShell(
        string viewFile,
        string backCommand,
        string bodyText)
    {
        var document = XDocument.Load(FindSourcePath("Views", viewFile));
        var shell = document.Descendants()
            .Single(element => element.Name.LocalName == "SettingsPageShell"
                && element.Descendants(Presentation + "TextBlock").Any(text =>
                    (string?)text.Attribute("Text") == bodyText));
        Assert.Equal(backCommand, (string?)shell.Attribute("BackCommand"));
        Assert.Empty(shell.Descendants(Presentation + "ScrollViewer"));
        Assert.Contains(shell.Descendants(Presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == bodyText);
        return shell;
    }

    private static void AssertTranslationListGutter(
        XDocument document,
        string itemsSource,
        string expectedContentMargin)
    {
        var items = document.Descendants(Presentation + "ItemsControl")
            .Single(element => (string?)element.Attribute("ItemsSource") == itemsSource);
        Assert.Equal(expectedContentMargin, (string?)items.Attribute("Margin"));
        AssertUsesGutter(items.Ancestors(Presentation + "ScrollViewer").Single());
    }

    private static void AssertUsesGutter(XElement scroller) => Assert.Equal(
        "{StaticResource FloatingToolsSpacingScrollContentGutter}",
        (string?)scroller.Attribute("Padding"));

    private static XElement Named(XDocument document, string localName, string name) =>
        document.Descendants()
            .Single(element => element.Name.LocalName == localName
                && (string?)element.Attribute(X + "Name") == name);

    private static string? AttributeValue(XElement element, string localName) =>
        (string?)element.Attributes().Single(attribute => attribute.Name.LocalName == localName);

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
