using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class CalendarEventsStageOneUiContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void EventsUsesSharedListInCompactRegionAndSeparateFullPage()
    {
        var document = LoadView();
        var page = document.Descendants(Presentation + "Grid")
            .Single(grid => (string?)grid.Attribute("Visibility") ==
                "{Binding IsEventsPage, Converter={StaticResource BooleanToVisibilityConverter}}");

        Assert.Contains(page.Descendants(), element =>
            element.Name.LocalName == "SettingsPageShell"
            && (string?)element.Attribute("BackCommand") == "{Binding BackToCalendarCommand}");
        var compact = Named(document, "Grid", "ContextualEventsPanel");
        Assert.Empty(compact.Descendants(Presentation + "ComboBox"));
        Assert.DoesNotContain(compact.Descendants(Presentation + "Button"), button =>
            ((string?)button.Attribute("Command"))?.Contains("Reset") == true);
        Assert.Contains(Named(document, "Border", "DayPanelExpandedContent"), compact.Ancestors());
        Assert.Contains(page.Descendants(Presentation + "ContentControl"), control =>
            (string?)control.Attribute("Content") == "{Binding EventsViewModel}");
        Assert.Contains(compact.Descendants(Presentation + "ContentControl"), control =>
            (string?)control.Attribute("Content") == "{Binding ContextualEventsViewModel}");
        var sort = Named(document, "Button", "ContextualEventsSortButton");
        var contextualTitle = Named(document, "TextBlock", "ContextualEventsTitleText");
        Assert.Equal("{Binding ContextualEventsViewModel.SortGlyph}", (string?)sort.Attribute("Content"));
        Assert.Equal("{Binding ContextualEventsViewModel.SortLabel}", (string?)sort.Attribute("ToolTip"));
        Assert.Equal("{Binding ContextualEventsTitle}", (string?)contextualTitle.Attribute("Text"));
        Assert.DoesNotContain(compact.Descendants(Presentation + "TextBox"), box =>
            ((string?)box.Attribute("Text"))?.Contains("Search", StringComparison.Ordinal) == true
            || ((string?)box.Attribute(X + "Name"))?.Contains("From", StringComparison.Ordinal) == true
            || ((string?)box.Attribute(X + "Name"))?.Contains("To", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void CompactHeaderContainsTitleSortAndCloseWithoutSortRowInSharedList()
    {
        var document = LoadView();
        var header = Named(document, "Grid", "ContextualEventsHeader");
        Assert.Empty(header.Elements(Presentation + "Grid.RowDefinitions"));
        Assert.Same(header, Named(document, "Button", "ContextualEventsSortButton").Parent);
        Assert.Same(header, Named(document, "Button", "ContextualEventsCloseButton").Parent);
        Assert.Single(header.Elements(Presentation + "Grid"));
        var list = document.Descendants(Presentation + "DataTemplate").Single(template =>
            (string?)template.Attribute(X + "Key") == "CalendarEventsListTemplate");
        Assert.DoesNotContain(list.Descendants(Presentation + "Button"), button =>
            ((string?)button.Attribute("Command"))?.Contains("SortCommand") == true);
        Assert.Null(Named(document, "GridSplitter", "DayPanelSplitter").Attribute("IsEnabled"));
    }

    [Fact]
    public void FullEventsTitleRemainsBoundToTheGeneralEventsHeading()
    {
        var document = LoadView();
        var shell = document.Descendants().Single(element =>
            element.Name.LocalName == "SettingsPageShell"
            && (string?)element.Attribute(X + "Name") == "CalendarEventsShell");

        Assert.Equal("{Binding EventsHeading}", (string?)shell.Attribute("Title"));
    }

    [Fact]
    public void HebrewContextualWeekSeparatorsUseTheContextualTitleForeground()
    {
        var document = LoadView();
        var separatorNames = new[]
        {
            "ContextualHebrewSameMonthWeekHeadingSeparatorText",
            "ContextualHebrewCrossMonthWeekRangeSeparatorText",
            "ContextualHebrewCrossMonthWeekHeadingSeparatorText"
        };

        Assert.All(separatorNames, name => Assert.Equal(
            "{DynamicResource FloatingToolsBrushForegroundSecondary}",
            (string?)Named(document, "TextBlock", name).Attribute("Foreground")));
        foreach (var panelName in new[]
                 {
                     "ContextualHebrewSameMonthWeekTitle",
                     "ContextualHebrewCrossMonthWeekTitle"
                 })
        {
            var panel = Named(document, "StackPanel", panelName);
            Assert.Equal("{DynamicResource FloatingToolsBrushForegroundSecondary}",
                (string?)panel.Attribute("TextElement.Foreground"));
            Assert.Equal("1", (string?)panel.Attribute("Opacity"));
        }
    }

    [Fact]
    public void ContextualEventsScrollerUsesTheSharedContentGutter()
    {
        var document = LoadView();
        var scroller = Named(document, "ScrollViewer", "ContextualEventsScrollViewer");

        Assert.Equal("{StaticResource FloatingToolsSpacingScrollContentGutter}",
            (string?)scroller.Attribute("Padding"));
        Assert.Equal("Auto", (string?)scroller.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scroller.Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void RowMutuallyShowsPreviewOrFullTextAndKeepsNumericDateLtr()
    {
        var document = LoadView();
        var row = Named(document, "Grid", "ContextualEventRow");
        var preview = Named(document, "TextBlock", "ContextualEventPreview");
        var full = Named(document, "TextBox", "ContextualEventFullText");
        var expand = Named(document, "Button", "ContextualEventExpandButton");
        var date = row.Descendants(Presentation + "TextBlock")
            .Single(text => (string?)text.Attribute("Text") == "{Binding DateText}");

        Assert.Equal("ContextualEventRow_OnMouseLeftButtonDown",
            (string?)row.Attribute("MouseLeftButtonDown"));
        Assert.Equal("{Binding IsCollapsed, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)preview.Attribute("Visibility"));
        Assert.Equal("{Binding IsExpanded, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)full.Attribute("Visibility"));
        Assert.Equal("{Binding HasHiddenContent, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)expand.Attribute("Visibility"));
        Assert.Equal("{Binding TextFlowDirection}", (string?)preview.Attribute("FlowDirection"));
        Assert.Equal("{Binding TextAlignment}", (string?)preview.Attribute("TextAlignment"));
        Assert.Equal("{Binding TextFlowDirection}", (string?)full.Attribute("FlowDirection"));
        Assert.Equal("{Binding TextAlignment}", (string?)full.Attribute("TextAlignment"));
        Assert.Equal("LeftToRight", (string?)date.Attribute("FlowDirection"));
        Assert.Equal("Center", (string?)date.Attribute("TextAlignment"));
    }

    [Fact]
    public void QuickAddMovedImmediatelyAfterTodayAndBeforeSearch()
    {
        var document = LoadView();
        var menu = Named(document, "Border", "CalendarHeaderExpandedContent")
            .Element(Presentation + "StackPanel")!;
        var children = menu.Elements().ToArray();
        var today = Array.FindIndex(children, element =>
            (string?)element.Attribute("Command") == "{Binding GoToTodayCommand}");
        var quickAdd = Array.FindIndex(children, element =>
            (string?)element.Attribute("Command") == "{Binding BeginQuickAddCommand}");
        var search = Array.FindIndex(children, element =>
            element.DescendantsAndSelf().Any(candidate =>
                (string?)candidate.Attribute(X + "Name") == "CalendarSearchTextBox"));

        Assert.Equal(today + 1, quickAdd);
        Assert.Equal(quickAdd + 1, search);
        var events = Array.FindIndex(children, element =>
            (string?)element.Attribute("Command") == "{Binding OpenEventsCommand}");
        var settings = Array.FindIndex(children, element =>
            (string?)element.Attribute("Command") == "{Binding OpenSettingsCommand}");
        Assert.True(events > search);
        Assert.Equal(events + 1, settings);
        Assert.Equal("{Binding AddEventMenuLabel}",
            (string?)children[quickAdd].Attribute("Content"));
        Assert.DoesNotContain(menu.Elements(Presentation + "Button"), button =>
            (string?)button.Attribute("Command") == "{Binding OpenContextualEventsCommand}");
    }

    private static XDocument LoadView() =>
        XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));

    private static XElement Named(XDocument document, string name, string xName) =>
        document.Descendants(Presentation + name)
            .Single(element => (string?)element.Attribute(X + "Name") == xName);

    private static string FindSourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine([directory.FullName, "src", "FloatingTools.App", .. parts]);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
