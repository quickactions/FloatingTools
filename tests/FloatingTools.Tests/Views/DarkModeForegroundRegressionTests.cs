using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Controls;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Regression coverage for a real C3-era bug: several shared-shell elements
/// (PanelWindow's header title, the All-Tools/Toolbar icon strokes,
/// PanelWindow's size-menu highlight, ApplicationSettingsView's root
/// background/button foreground) still referenced color tokens via
/// StaticResource instead of DynamicResource. Because PanelWindow,
/// ToolbarWindow, and the tool views inside them are constructed exactly
/// once at startup and never recreated, a StaticResource color reference
/// resolves once — against whichever theme happened to be active at that
/// first construction — and then never updates again. If the app's
/// first-resolved theme is Light (e.g. System mode on a Light-themed
/// Windows install), every such reference bakes in Light's near-black
/// foreground permanently, so switching to Dark later repaints surfaces
/// correctly but leaves those foregrounds stuck dark-on-dark.
///
/// Each test below constructs the real style/template while Light is
/// active (reproducing "app started resolving Light"), then switches a
/// real ThemeService to Dark and asserts the foreground actually updates
/// and is legible against its own surface — the same shape of check that
/// caught the bug, rather than pinning exact hex values.
/// </summary>
[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class DarkModeForegroundRegressionTests
{
    [Fact]
    public void PanelWindowHeaderTitle_ForegroundUpdatesToLightOnDarkSwitch()
        => WithLightThenDarkSwitch((themeService, window) =>
        {
            var text = new TextBlock { Text = "All tools" };
            text.SetResourceReference(TextBlock.ForegroundProperty, "FloatingToolsBrushForegroundPrimary");
            var border = new Border();
            border.SetResourceReference(Border.BackgroundProperty, "FloatingToolsBrushSurfaceHeader");
            border.Child = text;
            window.Content = border;
            window.UpdateLayout();

            AssertStuckWhileLight(() => (SolidColorBrush)text.Foreground, ForegroundPrimaryLight);

            themeService.ApplyAppearance(AppAppearanceMode.Dark);
            window.UpdateLayout();

            AssertForegroundLighterThanSurface(
                (SolidColorBrush)text.Foreground,
                (SolidColorBrush)border.Background);
        });

    [Fact]
    public void ToolHeaderStyle_ForegroundAndChevronUpdateToLightOnDarkSwitch()
        => WithLightThenDarkSwitch((themeService, window) =>
        {
            var style = (Style)Application.Current.TryFindResource("FloatingToolsSharedToolHeaderStyle");
            var header = new ToolHeaderControl { Style = style, Title = "Translation Assistant" };
            header.SetResourceReference(ToolHeaderControl.ForegroundProperty, "FloatingToolsBrushForegroundSecondary");
            var border = new Border();
            border.SetResourceReference(Border.BackgroundProperty, "FloatingToolsBrushSurfaceHeader");
            border.Child = header;
            window.Content = border;
            window.UpdateLayout();

            themeService.ApplyAppearance(AppAppearanceMode.Dark);
            window.UpdateLayout();

            AssertForegroundLighterThanSurface(
                (SolidColorBrush)header.Foreground,
                (SolidColorBrush)border.Background);
            AssertForegroundLighterThanSurface(
                (SolidColorBrush)header.ChevronBrush!,
                (SolidColorBrush)border.Background);
        });

    [Fact]
    public void SettingsComboBoxStyle_ForegroundUpdatesToLightOnDarkSwitch()
        => WithLightThenDarkSwitch((themeService, window) =>
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/FloatingTools.App;component/SharedUi/Styles/SettingsComboBoxes.xaml",
                    UriKind.Absolute)
            };
            var comboBox = new ComboBox { Style = (Style)dict["SettingsComboBoxStyle"] };
            window.Content = comboBox;
            window.UpdateLayout();

            themeService.ApplyAppearance(AppAppearanceMode.Dark);
            window.UpdateLayout();

            AssertForegroundLighterThanSurface(
                (SolidColorBrush)comboBox.Foreground,
                (SolidColorBrush)comboBox.Background);
        });

    [Fact]
    public void CalendarToolHeaderStyle_ForegroundUpdatesToLightOnDarkSwitch()
        => WithLightThenDarkSwitch((themeService, window) =>
        {
            var calendarView = new CalendarToolView();
            var style = (Style)calendarView.Resources["CalendarToolHeaderStyle"];
            var header = new ToolHeaderControl { Style = style, Title = "Calendar" };
            window.Content = header;
            window.UpdateLayout();

            themeService.ApplyAppearance(AppAppearanceMode.Dark);
            window.UpdateLayout();

            var background = header.Background as SolidColorBrush
                ?? (SolidColorBrush)Application.Current.TryFindResource("FloatingToolsBrushSurfaceHeader");
            AssertForegroundLighterThanSurface((SolidColorBrush)header.Foreground, background);
        });

    [Fact]
    public void SharedMenuItemStyle_ForegroundUpdatesToLightOnDarkSwitch()
        => WithLightThenDarkSwitch((themeService, window) =>
        {
            var style = (Style)Application.Current.TryFindResource("FloatingToolsSharedMenuItemStyle");
            var menuItem = new MenuItem { Style = style, Header = "Settings" };
            var stack = new StackPanel();
            stack.Children.Add(menuItem);
            window.Content = stack;
            window.UpdateLayout();

            themeService.ApplyAppearance(AppAppearanceMode.Dark);
            window.UpdateLayout();

            var surface = (SolidColorBrush)Application.Current.TryFindResource("FloatingToolsBrushSurfaceHover");
            AssertForegroundLighterThanSurface((SolidColorBrush)menuItem.Foreground, surface);
        });

    [Theory]
    [InlineData("TranslationIconTemplate")]
    [InlineData("NotesIconTemplate")]
    [InlineData("QuickChatIconTemplate")]
    [InlineData("CalendarIconTemplate")]
    public void ToolbarIconTemplates_StrokeUpdatesToLightOnDarkSwitch(string templateKey)
        => WithLightThenDarkSwitch((themeService, window) =>
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/FloatingTools.App;component/Resources/SharedWindowStyles.xaml",
                    UriKind.Absolute)
            };
            var template = (DataTemplate)dict[templateKey];
            var content = (FrameworkElement)template.LoadContent();
            var border = new Border();
            border.SetResourceReference(Border.BackgroundProperty, "FloatingToolsBrushSurfaceElevated");
            border.Child = content;
            window.Content = border;
            window.UpdateLayout();

            themeService.ApplyAppearance(AppAppearanceMode.Dark);
            window.UpdateLayout();

            var path = FindVisualChild<Path>(content)
                ?? throw new InvalidOperationException("Icon template has no Path.");
            AssertForegroundLighterThanSurface(
                (SolidColorBrush)path.Stroke,
                (SolidColorBrush)border.Background);
        });

    // --- helpers ---

    private static readonly Color ForegroundPrimaryLight = Color.FromRgb(0x1C, 0x1C, 0x1E);

    private static void AssertStuckWhileLight(Func<SolidColorBrush> getBrush, Color expectedWhileLight)
    {
        Assert.Equal(expectedWhileLight, getBrush().Color);
    }

    private static void AssertForegroundLighterThanSurface(SolidColorBrush foreground, SolidColorBrush surface)
    {
        var fgLuminance = RelativeLuminance(foreground.Color);
        var surfaceLuminance = RelativeLuminance(surface.Color);

        Assert.True(
            fgLuminance > surfaceLuminance,
            $"Expected foreground {foreground.Color} to be lighter than surface {surface.Color} " +
            $"in Dark mode (luminance {fgLuminance:F3} vs {surfaceLuminance:F3}).");

        var contrast = (fgLuminance + 0.05) / (surfaceLuminance + 0.05);
        Assert.True(
            contrast >= 3.0,
            $"Expected at least a 3:1 contrast ratio between foreground {foreground.Color} and " +
            $"surface {surface.Color} in Dark mode; got {contrast:F2}:1.");
    }

    private static double RelativeLuminance(Color color)
    {
        double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var result = FindVisualChild<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Starts a real ThemeService in Light (reproducing "the app's
    /// first-resolved theme is Light"), lets the caller build real
    /// elements/styles against it, then hands control back so the caller
    /// can switch to Dark and assert the result — all inside one WPF
    /// dispatcher run.
    /// </summary>
    private static void WithLightThenDarkSwitch(Action<ThemeService, Window> body)
        => WpfTestApplication.Run(() =>
        {
            using var themeService = new ThemeService(
                AppAppearanceMode.Light,
                new AppAppearanceResolver(() => AppTheme.Light),
                new NoopThemeChangeNotifier(),
                Application.Current.Resources);

            var window = new Window
            {
                Width = 200,
                Height = 200,
                Left = -5000,
                Top = -5000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                body(themeService, window);
            }
            finally
            {
                window.Close();
            }
        });

    private sealed class NoopThemeChangeNotifier : IWindowsThemeChangeNotifier
    {
        public event EventHandler? ThemeChanged { add { } remove { } }

        public void Start(IntPtr windowHandle)
        {
        }

        public void Dispose()
        {
        }
    }
}
