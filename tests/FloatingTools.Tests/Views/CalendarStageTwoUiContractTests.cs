using System.Xml.Linq;
using FloatingTools.App.Models;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.Views;

public sealed class CalendarStageTwoUiContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Calendar_IsRegisteredInToolMenuPanelAndToolbarIcon()
    {
        var panel = XDocument.Load(FindSourcePath("Views", "PanelWindow.xaml"));
        var toolbar = XDocument.Load(FindSourcePath("Views", "ToolbarWindow.xaml"));
        var resources = File.ReadAllText(
            FindSourcePath("Resources", "SharedWindowStyles.xaml"));

        Assert.Contains(panel.Descendants(Presentation + "Button"),
            element => (string?)element.Attribute("CommandParameter")
                == "{x:Static models:ToolId.Calendar}");
        Assert.Single(panel.Descendants(),
            element => element.Name.LocalName == "CalendarToolView");
        Assert.Contains(toolbar.Descendants(Presentation + "DataTrigger"),
            element => (string?)element.Attribute("Value")
                == "{x:Static models:ToolId.Calendar}");
        Assert.Contains("CalendarIconTemplate", resources);
    }

    [Fact]
    public void CalendarHeader_ReusesToolHeaderAndExposesStageThreeQuickAdd()
    {
        var view = XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));
        var header = view.Descendants()
            .Single(element => element.Name.LocalName == "ToolHeaderControl");

        Assert.Equal("{Binding HeaderTitle}", (string?)header.Attribute("Title"));
        Assert.Equal("+", (string?)header.Attribute("SecondaryActionContent"));
        Assert.Equal(
            "{Binding BeginQuickAddCommand}",
            (string?)header.Attribute("SecondaryActionCommand"));
        Assert.Equal(
            "{Binding ElementName=CalendarHeaderExpandedContent}",
            (string?)header.Attribute("ExpandedContentRoot"));

        var viewModel = new CalendarToolViewModel(
            new CalendarSettings(),
            new FloatingTools.App.Services.CalendarLanguageResolver(),
            new FloatingTools.App.Services.HebrewCalendarHolidayProvider());
        Assert.True(viewModel.BeginQuickAddCommand.CanExecute(null));
    }

    [Fact]
    public void NavigationGroupsArrowsAndKeepsOverflowOpposite()
    {
        var view = XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));
        var previous = view.Descendants(Presentation + "Button")
            .Single(element => (string?)element.Attribute("Command")
                == "{Binding NavigatePreviousCommand}");
        var next = view.Descendants(Presentation + "Button")
            .Single(element => (string?)element.Attribute("Command")
                == "{Binding NavigateNextCommand}");
        var stack = Assert.Single(previous.Ancestors(Presentation + "StackPanel"));

        Assert.Contains(next, stack.Elements(Presentation + "Button"));
        Assert.Equal("0", (string?)stack.Attribute("Grid.Column"));
        var overflow = view.Descendants(Presentation + "Button")
            .Single(element => (string?)element.Attribute("Click")
                == "ViewSelectorButton_OnClick");
        Assert.Equal("2", (string?)overflow.Attribute("Grid.Column"));
    }

    [Fact]
    public void ViewSelector_UsesAnchoredPopupAndOnlyMonthWeekOptions()
    {
        var xaml = XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));
        var code = File.ReadAllText(
            FindSourcePath("Views", "CalendarToolView.xaml.cs"));

        Assert.Contains("PopupAnchorService", code);
        Assert.Contains("new PopupAnchorRequest", code);
        Assert.Contains("CalendarRoot", code);
        Assert.Contains("CloseOnExternalClick = true", code);
        Assert.Contains("ShowMonthCommand", xaml.ToString());
        Assert.Contains("ShowWeekCommand", xaml.ToString());
        Assert.DoesNotContain("Year", xaml.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MonthUsesDynamicFiveOrSixBySevenGridAndNativeButtons()
    {
        var view = XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));
        var uniformGrids = view.Descendants(Presentation + "UniformGrid").ToArray();

        Assert.Contains(uniformGrids,
            grid => (string?)grid.Attribute("Rows") == "{Binding MonthWeekRowCount}"
                && (string?)grid.Attribute("Columns") == "7");
        Assert.Contains(view.Descendants(Presentation + "Button"),
            element => (string?)element.Attribute("Command")
                == "{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType={x:Type UserControl}}}"
                && (string?)element.Attribute("CommandParameter") == "{Binding Date}");
        Assert.DoesNotContain(view.Descendants(),
            element => element.Name.LocalName == "Calendar");
    }

    [Fact]
    public void WeekAndDayPanel_AreSiblingScrollingRegionsWithStageThreeSplitter()
    {
        var view = XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));
        var body = view.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute(X + "Name") == "CalendarBody");
        var scrolls = body.Descendants(Presentation + "ScrollViewer").ToArray();

        Assert.Equal(3, scrolls.Length);
        Assert.All(scrolls, scroll => Assert.Empty(
            scroll.Ancestors(Presentation + "ScrollViewer")));
        Assert.Contains(body.Descendants(Presentation + "Border"),
            content => (string?)content.Attribute(X + "Name") == "DayPanelExpandedContent"
                && (string?)content.Attribute("MinHeight") == "92"
                && (string?)content.Attribute("MaxHeight") == "268");
        Assert.Single(body.Descendants(),
            element => element.Name.LocalName == "GridSplitter");
    }

    [Fact]
    public void HeaderSearchAndSettings_AreFunctionalStageThreeBindings()
    {
        var view = XDocument.Load(FindSourcePath("Views", "CalendarToolView.xaml"));

        Assert.Contains(view.Descendants(Presentation + "TextBox"),
            element => (string?)element.Attribute("Text")
                    == "{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
                && (string?)element.Attribute("PreviewKeyDown")
                    == "CalendarSearchTextBox_OnPreviewKeyDown");
        Assert.Contains(view.Descendants(Presentation + "Button"),
            element => (string?)element.Attribute("Content") == "{Binding SettingsLabel}"
                && (string?)element.Attribute("Command") == "{Binding OpenSettingsCommand}");
    }

    [Fact]
    public void StageThreeUsesOneInlineEditorAndNoSeparateEventWindow()
    {
        var view = File.ReadAllText(FindSourcePath("Views", "CalendarToolView.xaml"));
        var viewModel = File.ReadAllText(
            FindSourcePath("ViewModels", "CalendarToolViewModel.cs"));
        var combined = view + viewModel;

        Assert.Contains("EventEditorTextBox", combined);
        Assert.Contains("BeginAddEventCommand", combined);
        Assert.Contains("BeginEditEventCommand", combined);
        Assert.Contains("DeleteEventCommand", combined);
        Assert.Contains("CopyEventCommand", combined);
        Assert.DoesNotContain("CalendarEventWindow", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarToolNameAndEnumRegistration_AreStable()
    {
        var viewModel = new FloatingToolbarViewModel();

        viewModel.SelectToolCommand.Execute(ToolId.Calendar);

        Assert.Equal(ToolId.Calendar, viewModel.ActiveTool);
        Assert.Equal("Calendar", viewModel.ActiveToolName);
    }

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
