using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class CalendarStageThreeUiContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void TodayRow_UsesSubtleHoverAndPhysicalLanguageAlignment()
    {
        var document = LoadView();
        var style = FindStyle(document, "CalendarTodayButtonStyle");
        var todayButton = document.Descendants(Presentation + "Button")
            .Single(button => (string?)button.Attribute("Command") == "{Binding GoToTodayCommand}"
                && (string?)button.Attribute("HorizontalContentAlignment")
                    == "{Binding ContentHorizontalAlignment}");

        Assert.Contains("{DynamicResource FloatingToolsBrushOverlayHover}", style.ToString());
        Assert.Equal(
            "{Binding ContentFlowDirection}",
            (string?)todayButton.Descendants(Presentation + "TextBlock")
                .Single().Attribute("FlowDirection"));
    }

    [Fact]
    public void EmptyPeriodSpaceClearsSelectionWithoutOwningOtherRegions()
    {
        var document = LoadView();
        var period = document.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute(X + "Name") == "CalendarPeriodRegion");
        var code = File.ReadAllText(FindSourcePath("Views", "CalendarToolView.xaml.cs"));

        Assert.Equal(
            "CalendarPeriodRegion_OnMouseLeftButtonDown",
            (string?)period.Attribute("MouseLeftButtonDown"));
        Assert.Contains("HasInteractiveAncestor", code);
        Assert.Contains("ClearSelectionCommand", code);
        Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute("MouseLeftButtonDown")
                == "CalendarPeriodRegion_OnMouseLeftButtonDown");
    }

    [Fact]
    public void DayPanelSplitter_HasBoundedExpandedContentAndDoesNotResizeWindow()
    {
        var document = LoadView();
        var content = document.Descendants(Presentation + "Border")
            .Single(element => (string?)element.Attribute(X + "Name") == "DayPanelExpandedContent");
        var splitter = document.Descendants(Presentation + "GridSplitter").Single();
        var code = File.ReadAllText(FindSourcePath("Views", "CalendarToolView.xaml.cs"));

        Assert.Null(content.Attribute("Height"));
        Assert.Null(content.Attribute("MaxHeight"));
        Assert.Contains("CalendarDayPanelMinimumHeightConverter", content.ToString());
        Assert.Contains("Path=\"LayoutMode\"", content.ToString());
        Assert.Contains("Path=\"IsEventEditorOpen\"", content.ToString());
        Assert.Contains("CalendarDayPanelSizing.GetMaximumHeight", code);
        Assert.Contains("CalendarDayPanelSizing.GetDefaultHeight", code);
        Assert.Equal("Rows", (string?)splitter.Attribute("ResizeDirection"));
        Assert.Equal("PreviousAndNext", (string?)splitter.Attribute("ResizeBehavior"));
        Assert.DoesNotContain("Window.Height", code);
    }

    [Fact]
    public void EventList_UsesOneInternalDayPanelScrollerAndReservedHoverActionColumn()
    {
        var document = LoadView();
        var dayScroll = document.Descendants(Presentation + "ScrollViewer")
            .Single(element => (string?)element.Attribute(X + "Name") == "DayPanelScrollViewer");
        var events = document.Descendants(Presentation + "ItemsControl")
            .Single(element => (string?)element.Attribute(X + "Name") == "DayPanelEventItems");
        var row = events.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute(X + "Name") == "EventRow");
        var columns = row.Descendants(Presentation + "ColumnDefinition").ToArray();
        var actionStyle = FindStyle(document, "CalendarEventActionsButtonStyle");

        Assert.Contains(events, dayScroll.Descendants());
        Assert.Equal("28", (string?)columns[1].Attribute("Width"));
        Assert.Equal("0", SetterValue(actionStyle, "Opacity"));
        Assert.Equal("False", SetterValue(actionStyle, "IsHitTestVisible"));
        Assert.Contains("IsMouseOver, ElementName=EventRow", actionStyle.ToString());
    }

    [Fact]
    public void EventAndViewActionsUseSharedAnchoredPopupInfrastructure()
    {
        var document = LoadView();
        var code = File.ReadAllText(FindSourcePath("Views", "CalendarToolView.xaml.cs"));

        Assert.Contains("PopupAnchorService", code);
        Assert.Contains("new PopupAnchorRequest", code);
        Assert.Contains("CalendarRoot", code);
        Assert.Contains("CloseOnExternalClick = true", code);
        Assert.Contains(document.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("Command") == "{Binding Owner.BeginEditEventCommand}");
        Assert.Contains(document.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("Command") == "{Binding Owner.CopyEventCommand}");
        Assert.Contains(document.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("Command") == "{Binding Owner.DeleteEventCommand}");
    }

    [Fact]
    public void TodaySelectedAndEventMarkersRemainIndependentMonthStates()
    {
        var document = LoadView();
        var style = FindStyle(document, "CalendarMonthDayButtonStyle");
        var text = style.ToString();

        Assert.Contains("IsToday", text);
        Assert.Contains("TodayMarker", text);
        Assert.Contains("IsSelected", text);
        Assert.Contains("FloatingToolsBrushSelection", text);
        Assert.Contains("HasEvents", text);
        Assert.Contains("EventMarker", text);
    }

    [Fact]
    public void SearchAndSettingsAreInternalFunctionalPages()
    {
        var document = LoadView();

        Assert.Contains(document.Descendants(Presentation + "TextBox"),
            box => (string?)box.Attribute("Text")
                == "{Binding SearchText, UpdateSourceTrigger=PropertyChanged}");
        Assert.Contains(document.Descendants(Presentation + "ItemsControl"),
            items => (string?)items.Attribute("ItemsSource") == "{Binding SearchResults}");
        Assert.Contains(document.Descendants(),
            element => element.Name.LocalName == "SettingsPageShell"
                && (string?)element.Attribute("BackCommand") == "{Binding BackToCalendarCommand}");
        var settings = document.Descendants().Single(element =>
            (string?)element.Attribute(X + "Name") == "CalendarSettingsShell");
        Assert.Equal(3, settings.Descendants(Presentation + "ComboBox").Count());
        Assert.Contains(document.Descendants(Presentation + "CheckBox"),
            box => (string?)box.Attribute("IsChecked") == "{Binding ShowHolidays, Mode=TwoWay}");
        Assert.DoesNotContain(document.Descendants(),
            element => element.Name.LocalName == "CalendarSettingsWindow");
    }

    [Fact]
    public void EventEditor_IsSingleLineAndSupportsKeyboardCommitCancellation()
    {
        var document = LoadView();
        var editor = document.Descendants(Presentation + "TextBox")
            .Single(element => (string?)element.Attribute(X + "Name") == "EventEditorTextBox");
        var code = File.ReadAllText(FindSourcePath("Views", "CalendarToolView.xaml.cs"));

        Assert.Null(editor.Attribute("AcceptsReturn"));
        Assert.Equal("EventEditorTextBox_OnPreviewKeyDown",
            (string?)editor.Attribute("PreviewKeyDown"));
        Assert.Contains("Key.Enter", code);
        Assert.Contains("SaveEventCommand", code);
        Assert.Contains("Key.Escape", code);
        Assert.Contains("CancelEventEditorCommand", code);
    }

    [Fact]
    public void ApplicationStartupWiresExistingStoresAndAwaitsCalendarInitialization()
    {
        var app = File.ReadAllText(FindSourcePath("App.xaml.cs"));

        Assert.Contains("settings,\n            settingsService,", Normalize(app));
        Assert.Contains("new JsonCalendarStore()", app);
        Assert.Contains("new WindowsClipboardService()", app);
        Assert.Contains("await calendarToolViewModel.InitializeAsync();", app);
        Assert.DoesNotContain("CalendarEntryStore", app);
    }

    private static XDocument LoadView() =>
        XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));

    private static XElement FindStyle(XDocument document, string key) =>
        document.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(X + "Key") == key);

    private static string? SetterValue(XElement style, string property) =>
        (string?)style.Elements(Presentation + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == property)
            .Attribute("Value");

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

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
