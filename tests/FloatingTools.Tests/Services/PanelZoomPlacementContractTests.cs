using System.IO;

namespace FloatingTools.Tests.Services;

public sealed class PanelZoomPlacementContractTests
{
    [Fact]
    public void Coordinator_UsesOneAuthoritativeZoomLayoutAndOneCoordinatedPlacement()
    {
        var coordinator = File.ReadAllText(FindSourcePath(
            "Services",
            "WindowCoordinator.cs"));
        var panel = File.ReadAllText(FindSourcePath(
            "Views",
            "PanelWindow.xaml.cs"));
        var placement = File.ReadAllText(FindSourcePath(
            "Platform",
            "Windows",
            "WindowPlacementService.cs"));

        Assert.Equal(
            1,
            CountOccurrences(
                coordinator,
                "PanelZoomCalculator.CalculateFixedHostLayout("));
        Assert.Contains("PanelWindow.PrepareVisibleLayout(", coordinator);
        Assert.Contains("_placementService.PlaceConnectedWindows(", coordinator);
        Assert.DoesNotContain("PanelWindow.UpdateLayout();", coordinator);
        Assert.DoesNotContain("availableHeightDip", coordinator);
        Assert.DoesNotContain("PanelZoomCalculator.CalculateFixedHostLayout(", panel);
        Assert.Contains("WindowPlacementService.RequiresConnectedPlacement(", coordinator);
        Assert.Contains("BeginDeferWindowPos(2)", placement);
        Assert.Contains("DeferWindowPos(", placement);
        Assert.Contains("EndDeferWindowPos(", placement);
        Assert.Contains("DispatcherPriority.ContextIdle", coordinator);
        Assert.Contains("FlushPendingSettingsSave();", coordinator);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = source.IndexOf(
                   value,
                   startIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }

    private static string FindSourcePath(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var pathSegments = new[]
            {
                directory.FullName,
                "src",
                "FloatingTools.App"
            }.Concat(segments).ToArray();
            var path = Path.Combine(pathSegments);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Could not find {Path.Combine(segments)}.");
    }
}