using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class CalendarFocusedCorrectionUiContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void DayPanelUsesOneCollapsibleHandleAndPreservesExpandedBounds()
    {
        var document = LoadView();
        var period = Named(document, "Grid", "CalendarPeriodRegion");
        var overlay = Named(document, "Grid", "DayPanelOverlay");
        var expanded = Named(document, "Border", "DayPanelExpandedContent");
        var splitter = Named(document, "GridSplitter", "DayPanelSplitter");
        var dayScroller = Named(document, "ScrollViewer", "DayPanelScrollViewer");

        Assert.Equal((string?)period.Attribute("Grid.Row"), (string?)overlay.Attribute("Grid.Row"));
        Assert.Equal("Bottom", (string?)overlay.Attribute("VerticalAlignment"));
        Assert.Equal("10", (string?)overlay.Attribute("Panel.ZIndex"));
        Assert.Equal("17", (string?)splitter.Attribute("Height"));
        Assert.Equal("106", (string?)expanded.Attribute("Height"));
        Assert.Equal("92", (string?)expanded.Attribute("MinHeight"));
        Assert.Equal("268", (string?)expanded.Attribute("MaxHeight"));
        Assert.Equal("{Binding IsDayPanelExpanded, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)expanded.Attribute("Visibility"));
        Assert.Equal("DayPanelSplitter_OnDragDelta", (string?)splitter.Attribute("DragDelta"));
        Assert.Equal("DayPanelSplitter_OnPreviewMouseLeftButtonUp",
            (string?)splitter.Attribute("PreviewMouseLeftButtonUp"));
        Assert.Contains("Value=\"˅\"", splitter.ToString());
        Assert.Contains("Value=\"˄\"", splitter.ToString());
        Assert.Contains(dayScroller.Ancestors(), ancestor => ReferenceEquals(ancestor, overlay));
    }

    [Fact]
    public void DayPanelHeaderMirrorsOnlyItsPlacementRowAndKeepsExplicitTextDirection()
    {
        var document = LoadView();
        var title = Named(document, "TextBlock", "DayPanelDateTitle");
        var add = Named(document, "Button", "DayPanelAddButton");
        var row = title.Parent!;

        Assert.Equal("LeftToRight", (string?)document.Root!.Attribute("FlowDirection"));
        Assert.Equal("{Binding ContentFlowDirection}", (string?)row.Attribute("FlowDirection"));
        Assert.Equal("{Binding ContentFlowDirection}", (string?)title.Attribute("FlowDirection"));
        Assert.Equal("{Binding ContentTextAlignment}", (string?)title.Attribute("TextAlignment"));
        Assert.Equal("1", (string?)add.Attribute("Grid.Column"));
    }

    [Fact]
    public void WeekKeepsVerticalDaysButWrapsOnlyUserEntriesWithExplicitEntryDirection()
    {
        var document = LoadView();
        var weekDays = Named(document, "ItemsControl", "WeekDayItems");
        var entries = Named(document, "ItemsControl", "WeekEventItems");
        var entryText = entries.Descendants(Presentation + "TextBox").Single();

        Assert.DoesNotContain(weekDays.Elements(Presentation + "ItemsControl.ItemsPanel"), _ => true);
        Assert.Contains(entries.Descendants(Presentation + "WrapPanel"),
            panel => (string?)panel.Attribute("Orientation") == "Horizontal");
        Assert.Equal(
            "{Binding DataContext.ContentFlowDirection, RelativeSource={RelativeSource AncestorType={x:Type UserControl}}}",
            (string?)entries.Attribute("FlowDirection"));
        Assert.Equal("{Binding FlowDirection}", (string?)entryText.Attribute("FlowDirection"));
        Assert.Equal("{Binding TextAlignment}", (string?)entryText.Attribute("TextAlignment"));
        Assert.Equal("2147483647", (string?)entries.Attribute("AlternationCount"));
        var separator = entries.Descendants(Presentation + "TextBlock")
            .Single(text => (string?)text.Attribute("Text") == "•");
        Assert.Contains("ItemsControl.AlternationIndex", separator.ToString());
        Assert.Contains("Value=\"0\"", separator.ToString());
    }

    [Fact]
    public void EditorAndEntryRowsUseOneContinuousHoverSurfaceWithoutChangingActionWidth()
    {
        var document = LoadView();
        var editor = Named(document, "TextBox", "EventEditorTextBox");
        var inputStyle = document.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(X + "Key") == "CalendarSingleLineInputStyle");
        var eventItems = Named(document, "ItemsControl", "DayPanelEventItems");
        var eventSection = Named(document, "Border", "DayPanelEventsSection");
        var eventRow = eventItems.Descendants(Presentation + "Grid")
            .Single(grid => (string?)grid.Attribute(X + "Name") == "EventRow");
        var eventSurface = eventRow.Descendants(Presentation + "Border")
            .Single(border => (string?)border.Attribute(X + "Name") == "EventSurface");

        Assert.Equal("{StaticResource CalendarSingleLineInputStyle}",
            (string?)editor.Attribute("Style"));
        Assert.DoesNotContain(document.Descendants(Presentation + "TextBlock"),
            text => (string?)text.Attribute("Text") == "{Binding EventPlaceholder}");
        Assert.Contains("Property=\"Padding\" Value=\"7,0\"", inputStyle.ToString());
        Assert.Contains("VerticalScrollBarVisibility", inputStyle.ToString());
        Assert.Contains("Disabled", inputStyle.ToString());
        Assert.Contains("FloatingToolsBrushSurfaceInput", eventSection.ToString());
        Assert.Contains(document.Descendants(Presentation + "TextBlock"),
            text => (string?)text.Attribute("Text") == "{Binding EventsHeading}");
        Assert.Equal("Transparent", (string?)eventRow.Attribute("Background"));
        Assert.Equal("2", (string?)eventSurface.Attribute("Grid.ColumnSpan"));
        Assert.Equal("False", (string?)eventSurface.Attribute("IsHitTestVisible"));
        Assert.Contains("Value=\"Transparent\"", eventSurface.ToString());
        Assert.Contains("FloatingToolsBrushSurfaceSubtleHover", eventSurface.ToString());
        var action = eventRow.Descendants(Presentation + "Button").Single();
        var actionStyle = document.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(X + "Key") == "CalendarEventActionsButtonStyle");
        Assert.Equal("1", (string?)action.Attribute("Grid.Column"));
        Assert.Contains("IsMouseOver, ElementName=EventRow", actionStyle.ToString());
        Assert.Contains("Property=\"IsHitTestVisible\" Value=\"True\"", actionStyle.ToString());
        Assert.Equal("28", (string?)eventRow.Descendants(Presentation + "ColumnDefinition")
            .Last().Attribute("Width"));
    }

    [Fact]
    public void AddEditorUsesASeparateDividerAndScrollsTheCompleteActionBlock()
    {
        var document = LoadView();
        var editorSection = Named(document, "Border", "EventEditorSection");
        var code = File.ReadAllText(FindSourcePath("Views", "CalendarToolView.xaml.cs"));

        Assert.Contains("Binding=\"{Binding IsAddingEvent}\" Value=\"True\"",
            editorSection.ToString());
        Assert.Contains("Property=\"BorderThickness\" Value=\"0,1,0,0\"",
            editorSection.ToString());
        Assert.Contains("FloatingToolsBrushBorderDefault", editorSection.ToString());
        Assert.NotNull(Named(document, "Button", "EventSaveButton"));
        Assert.NotNull(Named(document, "Button", "EventCancelButton"));
        Assert.Contains("ScrollEventEditorIntoView(EventEditorSection)", code);
        Assert.Contains("editorBlock.BringIntoView", code);
        Assert.DoesNotContain("ScrollEventEditorIntoView(textBox)", code);
        Assert.Contains("DayPanelScrollViewer.ScrollToVerticalOffset(targetOffset)", code);
    }

    [Fact]
    public void SettingsSearchTodayAndScrollersUseLocalLanguageAndSharedSpacingContracts()
    {
        var document = LoadView();
        var settingsShell = document.Descendants()
            .Single(element => element.Name.LocalName == "SettingsPageShell");
        var settingLabels = new[]
        {
            "{Binding LanguageLabel}",
            "{Binding DefaultViewLabel}",
            "{Binding FirstDayLabel}"
        }.Select(binding => document.Descendants(Presentation + "TextBlock")
            .Single(text => (string?)text.Attribute("Text") == binding));
        var search = Named(document, "TextBox", "CalendarSearchTextBox");
        var expanded = Named(document, "Border", "CalendarHeaderExpandedContent");
        var today = expanded.Descendants(Presentation + "TextBlock")
            .Single(text => (string?)text.Attribute("Text") == "{Binding TodayIndicatorText}");
        var scopedScrollers = new[]
        {
            Named(document, "ScrollViewer", "DayPanelScrollViewer"),
            Named(document, "ScrollViewer", "MonthScrollViewer"),
            Named(document, "ScrollViewer", "WeekScrollViewer"),
            Named(document, "ScrollViewer", "SearchResultsScrollViewer")
        };

        Assert.Equal("{Binding ContentFlowDirection}",
            (string?)settingsShell.Attribute("TitleFlowDirection"));
        Assert.Equal("{Binding ContentFlowDirection}",
            (string?)settingsShell.Attribute("HeaderFlowDirection"));
        Assert.Equal("{Binding ContentTextAlignment}",
            (string?)settingsShell.Attribute("TitleTextAlignment"));
        Assert.All(settingLabels, label => Assert.Equal(
            "{DynamicResource FloatingToolsBrushForegroundSecondary}",
            (string?)label.Attribute("Foreground")));
        Assert.Equal("{StaticResource CalendarSingleLineInputStyle}",
            (string?)search.Attribute("Style"));
        Assert.Equal("{Binding ContentFlowDirection}", (string?)search.Attribute("FlowDirection"));
        Assert.Equal("{Binding ContentFlowDirection}", (string?)today.Attribute("FlowDirection"));
        Assert.Equal("0,5", (string?)expanded.Elements(Presentation + "StackPanel")
            .Single().Attribute("Margin"));
        Assert.All(scopedScrollers, scroller =>
        {
            Assert.Equal(
                "{StaticResource FloatingToolsSpacingScrollContentGutter}",
                (string?)scroller.Attribute("Padding"));
            Assert.Equal("True",
                (string?)scroller.Attributes().Single(attribute =>
                    attribute.Name.LocalName == "SmoothWheelScrollBehavior.IsEnabled"));
        });
    }

    [Fact]
    public void MonthAndWeekUseTheSameOverlayAwareVerticalScrollingStrategy()
    {
        var document = LoadView();
        var period = Named(document, "Grid", "CalendarPeriodRegion");
        var overlay = Named(document, "Grid", "DayPanelOverlay");
        var monthItems = Named(document, "ItemsControl", "MonthDayItems");
        var weekItems = Named(document, "ItemsControl", "WeekDayItems");
        var monthScroller = Named(document, "ScrollViewer", "MonthScrollViewer");
        var monthLayer = Named(document, "Grid", "MonthContentLayer");
        var monthInset = Named(document, "Border", "MonthOverlayInset");
        var weekInset = Named(document, "Border", "WeekOverlayInset");

        Assert.All(new[] { monthInset, weekInset }, inset => Assert.Equal(
            "{Binding ActualHeight, ElementName=DayPanelOverlay}",
            (string?)inset.Attribute("Height")));
        Assert.Contains(monthInset.Ancestors(Presentation + "ScrollViewer"),
            scroller => ReferenceEquals(scroller, monthScroller));
        Assert.Contains(weekInset.Ancestors(Presentation + "ScrollViewer"),
            scroller => (string?)scroller.Attribute(X + "Name") == "WeekScrollViewer");
        Assert.Equal("Auto", (string?)monthScroller.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)monthScroller.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("{Binding ActualHeight, ElementName=CalendarPeriodRegion}",
            (string?)monthLayer.Attribute("Height"));
        Assert.Contains(monthItems.Ancestors(), ancestor => ReferenceEquals(ancestor, period));
        Assert.Contains(weekItems.Ancestors(), ancestor => ReferenceEquals(ancestor, period));
        Assert.Equal((string?)period.Attribute("Grid.Row"), (string?)overlay.Attribute("Grid.Row"));
        Assert.Equal(2, monthLayer.Descendants(Presentation + "RowDefinition").Count());
    }

    [Fact]
    public void EventActionsPopupIsCompactAndLocalizesOnlyItsTextAlignment()
    {
        var document = LoadView();
        var popup = Named(document, "Border", "CalendarEventActionsPopupSurface");
        var compactStyle = document.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(X + "Key") == "CalendarCompactPopupItemButtonStyle");
        var texts = popup.Descendants(Presentation + "TextBlock").ToArray();

        Assert.Equal("100", (string?)popup.Attribute("Width"));
        Assert.Equal("2", (string?)popup.Attribute("Padding"));
        Assert.Contains("Property=\"Height\" Value=\"28\"", compactStyle.ToString());
        Assert.Contains("Property=\"Padding\" Value=\"7,0\"", compactStyle.ToString());
        Assert.Equal(3, texts.Length);
        Assert.All(texts, text =>
        {
            Assert.Equal("{Binding Owner.ContentFlowDirection}",
                (string?)text.Attribute("FlowDirection"));
            Assert.Equal("{Binding Owner.ContentTextAlignment}",
                (string?)text.Attribute("TextAlignment"));
        });
        Assert.Null(popup.Attribute("FlowDirection"));
    }

    [Fact]
    public void OneSharedToolHeaderRemainsOutsideCalendarAndSettingsPages()
    {
        var document = LoadView();
        var header = Named(document, "ToolHeaderControl", "CalendarToolHeader");
        var settings = document.Descendants(Presentation + "Grid")
            .Single(grid => (string?)grid.Attribute("Visibility") ==
                "{Binding IsSettingsPage, Converter={StaticResource BooleanToVisibilityConverter}}");
        var code = File.ReadAllText(FindSourcePath("ViewModels", "CalendarToolViewModel.cs"));

        Assert.Same(header.Parent, settings.Parent);
        Assert.Equal("0", (string?)header.Attribute("Grid.Row"));
        Assert.Equal("1", (string?)settings.Attribute("Grid.Row"));
        Assert.Equal("{Binding ToggleHeaderCommand}", (string?)header.Attribute("Command"));
        Assert.DoesNotContain("if (IsSettingsPage)", code);
    }

    [Fact]
    public void WeekOmitsEmptyDayTextWhileDayPanelKeepsItsOwnEmptyState()
    {
        var document = LoadView();
        var weekItems = Named(document, "ItemsControl", "WeekDayItems");
        var dayScroller = Named(document, "ScrollViewer", "DayPanelScrollViewer");

        Assert.DoesNotContain(weekItems.Descendants(Presentation + "TextBlock"), text =>
            ((string?)text.Attribute("Text"))?.Contains("NoEventsText", StringComparison.Ordinal) == true);
        Assert.Contains(dayScroller.Descendants(Presentation + "TextBlock"), text =>
            (string?)text.Attribute("Text") == "{Binding DayPanel.NoEventsText}");
    }

    [Fact]
    public void SearchResultsStretchAcrossTheButtonAndUseCalendarLanguageAlignment()
    {
        var document = LoadView();
        var button = Named(document, "Button", "SearchResultButton");
        var content = Named(document, "StackPanel", "SearchResultContent");
        var date = Named(document, "TextBlock", "SearchResultDateText");
        var entry = Named(document, "TextBlock", "SearchResultEventText");
        const string flowBinding =
            "{Binding DataContext.ContentFlowDirection, RelativeSource={RelativeSource AncestorType={x:Type UserControl}}}";
        const string alignmentBinding =
            "{Binding DataContext.ContentTextAlignment, RelativeSource={RelativeSource AncestorType={x:Type UserControl}}}";

        Assert.Equal("Stretch", (string?)button.Attribute("HorizontalContentAlignment"));
        Assert.Equal("Stretch", (string?)content.Attribute("HorizontalAlignment"));
        Assert.Equal(flowBinding, (string?)content.Attribute("FlowDirection"));
        Assert.All(new[] { date, entry }, text =>
        {
            Assert.Equal(flowBinding, (string?)text.Attribute("FlowDirection"));
            Assert.Equal(alignmentBinding, (string?)text.Attribute("TextAlignment"));
        });
    }

    [Fact]
    public void PeriodTitleIsCenteredAgainstFullCalendarWidth()
    {
        var document = LoadView();
        var title = Named(document, "TextBlock", "PeriodTitleText");
        var navigationRow = title.Parent!;
        var selector = Named(document, "Button", "ViewSelectorButton");

        Assert.Equal("Center", (string?)title.Attribute("HorizontalAlignment"));
        Assert.Equal("3", (string?)title.Attribute("Grid.ColumnSpan"));
        Assert.Equal("Right", (string?)selector.Attribute("HorizontalAlignment"));
        Assert.Equal(3, navigationRow.Descendants(Presentation + "ColumnDefinition").Count());
    }

    [Fact]
    public void PopupsUseOneConnectedCalendarHostAndCalendarClampBounds()
    {
        var document = LoadView();
        var code = File.ReadAllText(FindSourcePath("Views", "CalendarToolView.xaml.cs"));

        Assert.NotNull(Named(document, "AnchoredPopupHost", "CalendarPopupHost"));
        Assert.Contains("new PopupAnchorService(() => CalendarPopupHost)", code);
        Assert.Contains("new PopupAnchorRequest(target, content, CalendarRoot)", code);
        Assert.Contains("PopupAnchorPreferredPlacement.Below", code);
    }

    private static XDocument LoadView() =>
        XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));

    private static XElement Named(XDocument document, string localName, string name) =>
        document.Descendants().Single(element => element.Name.LocalName == localName
            && (string?)element.Attribute(X + "Name") == name);

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
