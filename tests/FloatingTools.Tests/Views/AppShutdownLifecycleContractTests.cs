using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class AppShutdownLifecycleContractTests
{
    [Fact]
    public void AppUsesExplicitShutdownModeWithoutForcedProcessTermination()
    {
        var appXaml = XDocument.Load(FindSourcePath("App.xaml"));
        var appCode = File.ReadAllText(FindSourcePath("App.xaml.cs"));
        var coordinator = File.ReadAllText(
            FindSourcePath("Services", "WindowCoordinator.cs"));

        Assert.Equal(
            "OnExplicitShutdown",
            (string?)appXaml.Root?.Attribute("ShutdownMode"));
        Assert.Contains("Application.Current?.Shutdown()", coordinator);
        Assert.Contains("MainWindow = toolbarWindow;", appCode);
        Assert.Contains("_localOcrService?.Dispose();", appCode);
        Assert.DoesNotContain("Environment.Exit", appCode);
        Assert.DoesNotContain("Process.Kill", appCode);
        Assert.DoesNotContain("Dispatcher.InvokeShutdown", coordinator);
        Assert.DoesNotContain("Task.Delay", coordinator);

        var panelClose = coordinator.IndexOf(
            "CloseWindow(PanelWindow",
            StringComparison.Ordinal);
        var toolbarClose = coordinator.IndexOf(
            "CloseWindow(ToolbarWindow",
            StringComparison.Ordinal);
        Assert.True(panelClose >= 0);
        Assert.True(toolbarClose > panelClose);
    }

    private static string FindSourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "FloatingTools.sln");
            if (File.Exists(solution))
            {
                return Path.Combine(
                    [directory.FullName, "src", "FloatingTools.App", .. parts]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the source tree.");
    }
}
