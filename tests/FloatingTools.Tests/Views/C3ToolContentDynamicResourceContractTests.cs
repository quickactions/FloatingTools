using System.Text.RegularExpressions;

namespace FloatingTools.Tests.Views;

/// <summary>
/// C3 migrated tool-content color/brush references in Translation, Notes,
/// QuickChat, Calendar, and LinkListBlockControl to DynamicResource so tool
/// interiors live-retheme with the rest of the shell. This asserts each file
/// has no remaining color/brush StaticResource references (structural tokens
/// are exempt) and that no raw hex literal snuck back in outside the
/// deliberately-fixed "paper"/chip/hyperlink exceptions documented in the
/// C3 plan.
/// </summary>
public sealed class C3ToolContentDynamicResourceContractTests
{
    [Theory]
    [InlineData("Views/TranslationToolView.xaml")]
    [InlineData("Views/QuickChatToolView.xaml")]
    [InlineData("Views/CalendarToolView.xaml")]
    public void ToolView_ColorTokenReferences_AreDynamicResourceNotStaticResource(
        string relativePath)
    {
        var content = File.ReadAllText(FindSourcePath(relativePath));

        Assert.DoesNotContain("{StaticResource FloatingToolsBrush", content);
        Assert.DoesNotContain("{StaticResource FloatingToolsColor", content);
        Assert.Contains("{DynamicResource FloatingToolsBrush", content);
    }

    [Fact]
    public void NotesToolView_ColorTokenReferences_AreDynamicResourceExceptFixedPaperSurfaces()
    {
        var content = File.ReadAllText(FindSourcePath("Views/NotesToolView.xaml"));

        Assert.DoesNotContain("{StaticResource FloatingToolsBrush", content);
        Assert.Contains("{DynamicResource FloatingToolsBrush", content);

        // The text-block "paper" editor is deliberately theme-invariant
        // (fixed white surface, fixed dark text/caret/selection) — these 3
        // literals are the only raw hex expected to remain in this file.
        var remainingHex = Regex.Matches(content, "#[0-9A-Fa-f]{6,8}")
            .Select(match => match.Value)
            .ToHashSet();
        Assert.Equal(
            new HashSet<string> { "#FF202124", "#FFB8BCC4" },
            remainingHex);
    }

    [Fact]
    public void LinkListBlockControl_ColorTokenReferences_AreDynamicResourceExceptFixedChipAndHyperlink()
    {
        var content = File.ReadAllText(
            FindSourcePath("Controls/LinkListBlockControl.xaml"));

        Assert.DoesNotContain("{StaticResource FloatingToolsBrush", content);
        Assert.Contains("{DynamicResource FloatingToolsBrush", content);

        // Deliberately theme-invariant: the draft-token "chip" (fixed white
        // background, fixed dark text/caret) and the hyperlink color (a
        // conventional fixed link-blue) — see C3 plan §8/§16.
        var remainingHex = Regex.Matches(content, "#[0-9A-Fa-f]{6,8}")
            .Select(match => match.Value)
            .ToHashSet();
        Assert.Equal(
            new HashSet<string> { "#FF202124", "#FF0563C1" },
            remainingHex);
    }

    [Fact]
    public void ScreenCaptureOverlayAndImageViewerBackdrops_WereNotMigratedByC3()
    {
        // Regression guard: the capture scrim/selection chrome and the image
        // viewer's dark backdrop were explicitly excluded from theme
        // migration (C2 §9, reaffirmed by C3) because they must stay legible
        // over arbitrary desktop/photo content regardless of app theme.
        var overlay = File.ReadAllText(
            FindSourcePath("Views/ScreenCaptureOverlayWindow.xaml"));
        var imageViewer = File.ReadAllText(
            FindSourcePath("Views/ImageViewerWindow.xaml"));

        Assert.Contains("Background=\"#33000000\"", overlay);
        Assert.Contains("Background=\"#08FFFFFF\"", overlay);
        Assert.Contains("Background=\"#DD252528\"", overlay);
        Assert.Contains("Background=\"#F21B1B1E\"", imageViewer);
    }

    private static string FindSourcePath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "FloatingTools.App",
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
