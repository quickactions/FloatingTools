using System.Text.RegularExpressions;

namespace FloatingTools.Tests.Views;

public sealed class ThemeDynamicResourceScopeContractTests
{
    [Theory]
    [InlineData("SharedUi/Styles/Buttons.xaml")]
    [InlineData("SharedUi/Styles/ScrollBars.xaml")]
    [InlineData("SharedUi/Styles/ContextMenus.xaml")]
    [InlineData("SharedUi/Styles/TextBoxes.xaml")]
    [InlineData("SharedUi/Styles/Inputs.xaml")]
    [InlineData("SharedUi/Styles/ToolHeader.xaml")]
    [InlineData("SharedUi/Styles/Tooltips.xaml")]
    [InlineData("SharedUi/Styles/SettingsPageShell.xaml")]
    public void ColorAndBrushTokenReferences_AreDynamicResourceNotStaticResource(
        string relativePath)
    {
        var content = File.ReadAllText(FindSourcePath(relativePath));

        Assert.DoesNotContain("{StaticResource FloatingToolsBrush", content);
        Assert.DoesNotContain("{StaticResource FloatingToolsColor", content);
        Assert.Contains("{DynamicResource FloatingToolsBrush", content);
    }

    [Theory]
    [InlineData("SharedUi/Styles/Buttons.xaml")]
    [InlineData("SharedUi/Styles/ScrollBars.xaml")]
    [InlineData("SharedUi/Styles/ContextMenus.xaml")]
    [InlineData("SharedUi/Styles/TextBoxes.xaml")]
    [InlineData("SharedUi/Styles/Inputs.xaml")]
    [InlineData("SharedUi/Styles/ToolHeader.xaml")]
    [InlineData("SharedUi/Styles/Tooltips.xaml")]
    [InlineData("SharedUi/Styles/SettingsPageShell.xaml")]
    [InlineData("SharedUi/Styles/SettingsComboBoxes.xaml")]
    public void StructuralTokenReferences_RemainStaticResource(string relativePath)
    {
        var content = File.ReadAllText(FindSourcePath(relativePath));

        // Structural (spacing/radius/typography/size) tokens never vary by theme
        // and must not be paid the DynamicResource re-evaluation cost.
        foreach (Match match in Regex.Matches(
            content,
            @"\{(Static|Dynamic)Resource FloatingTools(Size|Spacing|Radius|Font|Stroke)[A-Za-z]*\}"))
        {
            Assert.StartsWith("{StaticResource", match.Value);
        }
    }

    [Fact]
    public void PanelWindow_ContentBackgrounds_AreDynamicResourceForSurfaceInput()
    {
        var content = File.ReadAllText(FindSourcePath("Views/PanelWindow.xaml"));

        Assert.DoesNotContain(
            "{StaticResource FloatingToolsBrushSurfaceInput}", content);
        Assert.Equal(
            2,
            Regex.Matches(
                content,
                Regex.Escape("{DynamicResource FloatingToolsBrushSurfaceInput}")).Count);
    }

    [Fact]
    public void PanelWindow_TileGeometryAndOtherStyling_IsUnchanged()
    {
        var content = File.ReadAllText(FindSourcePath("Views/PanelWindow.xaml"));

        // B2 tile geometry/style must be untouched by the theme pass.
        Assert.Contains("FloatingToolsSharedToolTileButtonStyle", content);
        Assert.Contains("Width=\"44\"", content);
        Assert.Contains("Margin=\"2\"", content);
    }

    [Fact]
    public void SettingsComboBoxes_TokenReferences_AreDynamicResourceNotStaticResource()
    {
        var content = File.ReadAllText(
            FindSourcePath("SharedUi/Styles/SettingsComboBoxes.xaml"));

        // C2 migrated every color/brush literal in this file (it had zero
        // token references pre-C2) to DynamicResource so the settings
        // ComboBoxes live-retheme with the rest of the shared chrome.
        Assert.DoesNotContain("{StaticResource FloatingToolsBrush", content);
        Assert.Contains("{DynamicResource FloatingToolsBrush", content);
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
