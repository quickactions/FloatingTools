using System.Xml.Linq;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Covers the Settings chrome follow-ups: the connection-test verdict belongs to
/// the API key it tested (not the model picker below it), it is themed with
/// semantic status tokens, and the toolbar shows a Settings icon while the
/// settings page is open instead of the last tool's icon.
/// </summary>
public sealed class SettingsChromeContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ConnectionStatus_SitsUnderTheApiKeyActionsAndAboveTheModelPicker()
    {
        var document = XDocument.Load(FindSourcePath("Views", "ApplicationSettingsView.xaml"));
        var ordered = document.Descendants().ToList();

        var status = ordered.Single(element =>
            (string?)element.Attribute(X + "Name") == "ApplicationConnectionStatusText");
        // The view has a "Test connection" button per credential scope; the one
        // that matters here is the application-scope button preceding the status.
        var testButton = ordered.Last(element =>
            (string?)element.Attribute("Content") == "Test connection"
            && ordered.IndexOf(element) < ordered.IndexOf(status));
        var modelLabel = ordered.Single(element =>
            (string?)element.Attribute("Text") == "Default Model");

        var statusIndex = ordered.IndexOf(status);
        Assert.True(
            ordered.IndexOf(testButton) < statusIndex,
            "The verdict must follow the API-key action row it belongs to.");
        Assert.True(
            statusIndex < ordered.IndexOf(modelLabel),
            "The verdict must not appear under the Default Model picker.");
    }

    [Fact]
    public void ConnectionStatus_UsesSemanticStatusTokensNotHardcodedColors()
    {
        var document = XDocument.Load(FindSourcePath("Views", "ApplicationSettingsView.xaml"));
        var status = document.Descendants().Single(element =>
            (string?)element.Attribute(X + "Name") == "ApplicationConnectionStatusText");
        var markup = status.ToString();

        Assert.Contains("FloatingToolsBrushForegroundMuted", markup);
        Assert.Contains("FloatingToolsBrushStatusSuccess", markup);
        Assert.Contains("FloatingToolsBrushStatusError", markup);
        Assert.Equal("Wrap", (string?)status.Attribute("TextWrapping"));
        Assert.DoesNotContain("#FF", markup);
    }

    [Theory]
    [InlineData("Colors.Dark.xaml")]
    [InlineData("Colors.Light.xaml")]
    public void BothPalettes_DefineTheSuccessStatusToken(string paletteFile)
    {
        var palette = File.ReadAllText(
            FindSourcePath("SharedUi", "Tokens", paletteFile));

        Assert.Contains("FloatingToolsColorStatusSuccess", palette);
        Assert.Contains("FloatingToolsBrushStatusSuccess", palette);
    }

    [Fact]
    public void Toolbar_ShowsTheSettingsIconWhileTheSettingsPageIsOpen()
    {
        var toolbar = File.ReadAllText(FindSourcePath("Views", "ToolbarWindow.xaml"));
        var sharedStyles = File.ReadAllText(
            FindSourcePath("Resources", "SharedWindowStyles.xaml"));

        Assert.Contains("ApplicationSettingsIconTemplate", sharedStyles);

        // The settings trigger must come after the per-tool icon triggers so it
        // wins while PanelState is ApplicationSettings.
        var calendarTrigger = toolbar.IndexOf(
            "CalendarIconTemplate", StringComparison.Ordinal);
        var settingsTrigger = toolbar.IndexOf(
            "ApplicationSettingsIconTemplate", StringComparison.Ordinal);
        Assert.True(calendarTrigger >= 0 && settingsTrigger > calendarTrigger);
        Assert.Contains("PanelState.ApplicationSettings", toolbar);
    }

    [Fact]
    public async Task ConnectionVerdict_TracksSuccessFailureAndIsClearedWithTheMessage()
    {
        var tester = new ScriptedConnectionTester();
        var viewModel = CreateSettingsViewModel(tester);

        Assert.Null(viewModel.ApplicationConnectionSucceeded);

        tester.Next = new ConnectionTestResult(false, "Invalid API key.");
        await viewModel.TestApplicationConnectionCommand.ExecuteAsync(null);
        Assert.Equal("Invalid API key.", viewModel.ApplicationConnectionStatusMessage);
        Assert.False(viewModel.ApplicationConnectionSucceeded);

        tester.Next = new ConnectionTestResult(true, "Connection verified.");
        await viewModel.TestApplicationConnectionCommand.ExecuteAsync(null);
        Assert.True(viewModel.ApplicationConnectionSucceeded);

        // Clearing the message must drop the verdict so no stale colour remains.
        viewModel.ApplicationConnectionStatusMessage = null;
        Assert.Null(viewModel.ApplicationConnectionSucceeded);
    }

    private static SettingsViewModel CreateSettingsViewModel(
        IOpenAiConnectionTester tester) =>
        new(
            new AppSettings(),
            new NoOpAppSettingsStore(),
            new EmptyApiKeyStore(),
            new TestOpenAiConfigurationProvider(),
            tester,
            _ => { },
            applicationConnectionTester: tester);

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

    private sealed class ScriptedConnectionTester : IOpenAiConnectionTester
    {
        public ConnectionTestResult Next { get; set; } = new(false, "not configured");

        public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Next);
    }

    private sealed class NoOpAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Load() => new();

        public bool Save(AppSettings settings) => true;
    }

    private sealed class EmptyApiKeyStore : ISecureApiKeyStore
    {
        public bool HasKey => false;

        public string? Load() => null;

        public void Save(string apiKey)
        {
        }

        public void Remove()
        {
        }
    }
}
