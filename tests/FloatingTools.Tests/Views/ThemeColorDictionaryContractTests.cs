using System.Windows;
using FloatingTools.Tests;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class ThemeColorDictionaryContractTests
{
    [Fact]
    public void DarkAndLightDictionaries_ExposeExactlyTheSameKeySet()
        => WpfTestApplication.Run(() =>
        {
            var dark = LoadDictionary("Colors.Dark.xaml");
            var light = LoadDictionary("Colors.Light.xaml");

            var darkKeys = dark.Keys.Cast<object>().Select(key => key.ToString()).ToHashSet();
            var lightKeys = light.Keys.Cast<object>().Select(key => key.ToString()).ToHashSet();

            Assert.Equal(darkKeys, lightKeys);
        });

    [Fact]
    public void ColorDictionaries_ContainNoParallelDarkOrLightPrefixedKeys()
        => WpfTestApplication.Run(() =>
        {
            var dark = LoadDictionary("Colors.Dark.xaml");

            Assert.All(dark.Keys.Cast<object>(), key =>
            {
                var name = key.ToString();
                Assert.DoesNotContain("Dark", name, StringComparison.Ordinal);
                Assert.DoesNotContain("Light", name, StringComparison.Ordinal);
            });
        });

    [Fact]
    public void ColorDictionaries_ExposeTheExpectedSemanticKeys()
        => WpfTestApplication.Run(() =>
        {
            var dark = LoadDictionary("Colors.Dark.xaml");

            // 32 Color entries + 32 matching SolidColorBrush entries (one per
            // color) + 2 opacity constants. C2 added 6 new semantic pairs
            // (SurfaceHeader, SurfaceSelected, StatusError, BorderDivider,
            // OverlayHover, OverlayPressed) on top of C1's original 18; C3
            // added 7 more (SurfaceMessageUser, AccentToday, AccentHoliday,
            // AccentFavorite, StatusWarning, Scrim, SurfaceInsertionHighlight);
            // StatusSuccess was added for the Settings connection-test verdict,
            // which needed a positive counterpart to StatusError.
            Assert.Equal(66, dark.Count);
            Assert.True(dark.Contains("FloatingToolsColorStatusSuccess"));
            Assert.True(dark.Contains("FloatingToolsBrushStatusSuccess"));
            Assert.True(dark.Contains("FloatingToolsColorSurfaceBase"));
            Assert.True(dark.Contains("FloatingToolsBrushForegroundPrimary"));
            Assert.True(dark.Contains("FloatingToolsBrushSurfaceHover"));
            Assert.True(dark.Contains("FloatingToolsBrushSurfacePressed"));
            Assert.True(dark.Contains("FloatingToolsBrushBorderDefault"));
            Assert.True(dark.Contains("FloatingToolsBrushSurfaceHeader"));
            Assert.True(dark.Contains("FloatingToolsBrushSurfaceSelected"));
            Assert.True(dark.Contains("FloatingToolsBrushSurfaceMessageUser"));
            Assert.True(dark.Contains("FloatingToolsBrushAccentToday"));
            Assert.True(dark.Contains("FloatingToolsBrushAccentHoliday"));
            Assert.True(dark.Contains("FloatingToolsBrushAccentFavorite"));
            Assert.True(dark.Contains("FloatingToolsBrushStatusWarning"));
            Assert.True(dark.Contains("FloatingToolsBrushScrim"));
            Assert.True(dark.Contains("FloatingToolsBrushSurfaceInsertionHighlight"));
            Assert.True(dark.Contains("FloatingToolsBrushStatusError"));
            Assert.True(dark.Contains("FloatingToolsBrushBorderDivider"));
            Assert.True(dark.Contains("FloatingToolsBrushOverlayHover"));
            Assert.True(dark.Contains("FloatingToolsBrushOverlayPressed"));
            Assert.True(dark.Contains("FloatingToolsOpacityDisabled"));
            Assert.True(dark.Contains("FloatingToolsOpacitySelection"));
        });

    [Fact]
    public void OpacityConstants_AreIdenticalAcrossBothDictionaries()
        => WpfTestApplication.Run(() =>
        {
            var dark = LoadDictionary("Colors.Dark.xaml");
            var light = LoadDictionary("Colors.Light.xaml");

            Assert.Equal(
                dark["FloatingToolsOpacityDisabled"],
                light["FloatingToolsOpacityDisabled"]);
            Assert.Equal(
                dark["FloatingToolsOpacitySelection"],
                light["FloatingToolsOpacitySelection"]);
        });

    private static ResourceDictionary LoadDictionary(string fileName) =>
        new()
        {
            Source = new Uri(
                $"pack://application:,,,/FloatingTools.App;component/SharedUi/Tokens/{fileName}",
                UriKind.Absolute)
        };
}
