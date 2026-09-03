using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class ApplicationSettingsPanelContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ToolbarContextMenu_OffersSettingsAlongsideExit()
    {
        var document = XDocument.Load(FindSourcePath("Views", "ToolbarWindow.xaml"));
        var menu = document.Descendants(Presentation + "ContextMenu")
            .Single(element => (string?)element.Attribute(X + "Key")
                == "ToolbarContextMenu");
        var items = menu.Elements(Presentation + "MenuItem").ToArray();

        Assert.Collection(
            items,
            settings =>
            {
                Assert.Equal("Settings", (string?)settings.Attribute("Header"));
                Assert.Equal(
                    "SettingsMenuItem_OnClick",
                    (string?)settings.Attribute("Click"));
            },
            exit =>
            {
                Assert.Equal("Exit", (string?)exit.Attribute("Header"));
                Assert.Equal("ExitMenuItem_OnClick", (string?)exit.Attribute("Click"));
            });
    }

    [Fact]
    public void ApplicationSettings_AreHostedByTheNormalPanelInfrastructure()
    {
        var panel = XDocument.Load(FindSourcePath("Views", "PanelWindow.xaml"));
        var coordinator = File.ReadAllText(
            FindSourcePath("Services", "WindowCoordinator.cs"));

        var settingsView = panel.Descendants()
            .Single(element => element.Name.LocalName == "ApplicationSettingsView");
        var settingsTrigger = settingsView.Descendants(Presentation + "DataTrigger")
            .Single();

        Assert.Equal("ApplicationSettings", (string?)settingsView.Attribute(X + "Name"));
        Assert.Equal(
            "{x:Static models:PanelState.ApplicationSettings}",
            (string?)settingsTrigger.Attribute("Value"));
        Assert.Contains("_viewModel.OpenApplicationSettingsCommand.Execute(null);",
            coordinator);
        Assert.Contains("ShowPanel();", ExtractSettingsHandler(coordinator));
        Assert.DoesNotContain("ApplicationSettingsWindow", coordinator);
        Assert.False(File.Exists(
            FindSourcePath("Views", "ApplicationSettingsWindow.xaml")));
    }

    [Fact]
    public void ApplicationSettingsPanel_ExposesApplicationAiBindings()
    {
        var document = XDocument.Load(
            FindSourcePath("Views", "ApplicationSettingsView.xaml"));

        Assert.Contains(document.Descendants(Presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Application AI");
        Assert.Contains(document.Descendants(Presentation + "TextBlock"),
            element => (string?)element.Attribute("Text")
                == "{Binding ApplicationProviderDisplayName}");
        Assert.Contains(document.Descendants(Presentation + "Button"),
            element => (string?)element.Attribute("Command")
                == "{Binding BeginApplicationApiKeyEditCommand}");
        Assert.Contains(document.Descendants(Presentation + "Button"),
            element => (string?)element.Attribute("Command")
                == "{Binding RemoveApplicationApiKeyCommand}");
        Assert.Contains(document.Descendants(Presentation + "Button"),
            element => (string?)element.Attribute("Command")
                == "{Binding TestApplicationConnectionCommand}");

        var model = document.Descendants(Presentation + "ComboBox")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding ApplicationDefaultModelOptions}");
        Assert.Equal(
            "{Binding ApplicationDefaultModelOptions}",
            (string?)model.Attribute("ItemsSource"));
        Assert.Equal("Model", (string?)model.Attribute("SelectedValuePath"));
        Assert.Equal(
            "{Binding SelectedApplicationDefaultModel, Mode=TwoWay}",
            (string?)model.Attribute("SelectedValue"));
        Assert.Equal(
            "{StaticResource SettingsComboBoxStyle}",
            (string?)model.Attribute("Style"));
        Assert.Equal(
            "{Binding DisplayName}",
            (string?)model.Descendants(Presentation + "TextBlock")
                .Single().Attribute("Text"));

        var language = document.Descendants(Presentation + "ComboBox")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding ApplicationLanguageChoices}");
        Assert.Equal("Value", (string?)language.Attribute("SelectedValuePath"));
        Assert.Equal(
            "{Binding SelectedApplicationLanguage, Mode=TwoWay}",
            (string?)language.Attribute("SelectedValue"));
        Assert.Equal(
            "{StaticResource SettingsComboBoxStyle}",
            (string?)language.Attribute("Style"));
    }

    [Fact]
    public void ApplicationSettingsPanel_ExposesAppearanceBinding()
    {
        var document = XDocument.Load(
            FindSourcePath("Views", "ApplicationSettingsView.xaml"));

        Assert.Contains(document.Descendants(Presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Appearance");

        var appearance = document.Descendants(Presentation + "ComboBox")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding AppearanceChoices}");
        Assert.Equal("Value", (string?)appearance.Attribute("SelectedValuePath"));
        Assert.Equal(
            "{Binding SelectedAppearance, Mode=TwoWay}",
            (string?)appearance.Attribute("SelectedValue"));
        Assert.Equal(
            "{StaticResource SettingsComboBoxStyle}",
            (string?)appearance.Attribute("Style"));
        Assert.Equal(
            "{Binding DisplayName}",
            (string?)appearance.Descendants(Presentation + "TextBlock")
                .Single().Attribute("Text"));
    }

    [Fact]
    public void PanelReceivesTheExistingSettingsViewModelWithoutAddingAToolTile()
    {
        var app = File.ReadAllText(FindSourcePath("App.xaml.cs"));
        var panelCode = File.ReadAllText(
            FindSourcePath("Views", "PanelWindow.xaml.cs"));
        var panel = XDocument.Load(FindSourcePath("Views", "PanelWindow.xaml"));

        Assert.Contains("notesToolViewModel,\n            quickChatViewModel,\n            calendarToolViewModel,\n            translationToolViewModel.Settings);",
            NormalizeLineEndings(app));
        Assert.Contains("ApplicationSettings.DataContext = applicationSettingsViewModel",
            panelCode);

        var toolParameters = panel.Descendants(Presentation + "Button")
            .Select(element => (string?)element.Attribute("CommandParameter"))
            .OfType<string>()
            .ToArray();
        Assert.Equal(
            [
                "{x:Static models:ToolId.Translation}",
                "{x:Static models:ToolId.Notes}",
                "{x:Static models:ToolId.QuickChat}",
                "{x:Static models:ToolId.Calendar}"
            ],
            toolParameters);
        Assert.DoesNotContain("ToolId.ApplicationSettings", panel.ToString());
    }

    [Fact]
    public void ExistingToolPanelsRemainRestrictedToActiveToolState()
    {
        var panel = XDocument.Load(FindSourcePath("Views", "PanelWindow.xaml"));

        foreach (var viewName in new[] { "TranslationToolView", "NotesToolView", "QuickChatToolView", "CalendarToolView" })
        {
            var view = panel.Descendants()
                .Single(element => element.Name.LocalName == viewName);
            var trigger = view.Descendants(Presentation + "MultiDataTrigger").Single();
            Assert.Contains(
                trigger.Descendants(Presentation + "Condition"),
                condition => (string?)condition.Attribute("Value")
                    == "{x:Static models:PanelState.ActiveTool}");
        }
    }

    [Fact]
    public void ExitPath_UsesTaskWorkflowWithAllPersistenceParticipants()
    {
        var toolbar = File.ReadAllText(
            FindSourcePath("Views", "ToolbarWindow.xaml.cs"));
        var coordinator = File.ReadAllText(
            FindSourcePath("Services", "WindowCoordinator.cs"));

        Assert.Contains("ExitRequested?.Invoke(this, EventArgs.Empty);", toolbar);
        var exitHandlerStart = coordinator.IndexOf(
            "private async void OnExitRequested", StringComparison.Ordinal);
        var nextHandlerStart = coordinator.IndexOf(
            "private async void OnPanelCloseRequested",
            exitHandlerStart,
            StringComparison.Ordinal);
        var exitHandler = coordinator[exitHandlerStart..nextHandlerStart];
        Assert.Contains("await RequestExitAsync();", exitHandler);
        Assert.Contains("internal Task RequestExitAsync()", coordinator);
        Assert.Contains("_notesToolViewModel.PrepareForExitAsync()", coordinator);
        Assert.Contains("_activeQuickChatConversation.PrepareForExitAsync()", coordinator);
        Assert.Contains("_quickChatViewModel.DisposeAsync().AsTask()", coordinator);
        Assert.Contains("_calendarToolViewModel.PrepareForExitAsync()", coordinator);
        Assert.Contains("CloseAll,", coordinator);
        Assert.Contains("Application.Current?.Shutdown()", coordinator);
    }

    private static string ExtractSettingsHandler(string coordinator)
    {
        var start = coordinator.IndexOf(
            "private async void OnSettingsRequested", StringComparison.Ordinal);
        var end = coordinator.IndexOf(
            "private async void OnExitRequested", start, StringComparison.Ordinal);
        return coordinator[start..end];
    }

    private static string NormalizeLineEndings(string value) =>
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
