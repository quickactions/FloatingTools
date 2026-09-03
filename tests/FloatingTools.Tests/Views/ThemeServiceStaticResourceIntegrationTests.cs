using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Regression coverage for the C1 startup crash: a real ToolbarWindow icon
/// DataTemplate (Resources/SharedWindowStyles.xaml, compiled/BAML) failed to
/// resolve a StaticResource FloatingTools brush reference the first time it
/// was instantiated, even though ThemeService had already added the color
/// dictionary to the merge collection and a plain indexer lookup found the
/// key. The failure was specific to Application.Current.Resources (WPF's
/// optimized template loader does not reliably see resources added there
/// purely via code after InitializeComponent()) and did not reproduce
/// against a local FrameworkElement.Resources tree, so this test exercises
/// the same shape of resource graph App.xaml now declares — a static
/// Colors.Dark.xaml baseline merged before ThemeService appends its own
/// managed dictionary — and asserts template instantiation succeeds.
/// None of the pre-existing C1 tests caught the real failure: ThemeServiceTests
/// exercises ThemeService against a bare throwaway ResourceDictionary (no
/// compiled templates involved), and WpfTestApplication's shared test
/// Application statically declares Colors.Dark.xaml in its own hardcoded
/// resource list, so it never modeled "only a code-appended color dictionary,
/// no static one," which is what actually broke in production.
/// </summary>
[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class ThemeServiceStaticResourceIntegrationTests
{
    [Fact]
    public void StaticBaselinePlusThemeServiceAppend_ResolvesSuccessfully()
        => WpfTestApplication.Run(() =>
        {
            var localResources = new ResourceDictionary();
            localResources.MergedDictionaries.Add(SharedWindowStylesDictionary());
            localResources.MergedDictionaries.Add(ColorsDictionary(AppTheme.Dark));

            using var themeService = new ThemeService(
                AppAppearanceMode.Dark,
                new AppAppearanceResolver(() => AppTheme.Dark),
                new StubThemeChangeNotifier(),
                localResources);

            var exception = Record.Exception(() =>
                ShowIconTemplateAndClose(localResources));

            Assert.Null(exception);
        });

    private static void ShowIconTemplateAndClose(ResourceDictionary resources)
    {
        var contentControl = new ContentControl
        {
            ContentTemplate = (DataTemplate)resources["TranslationIconTemplate"]
        };
        var root = new Grid { Resources = resources };
        root.Children.Add(contentControl);
        var window = new Window
        {
            Content = root,
            Width = 60,
            Height = 60,
            Left = -10_000,
            Top = -10_000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None
        };

        try
        {
            window.Show();
            window.UpdateLayout();
        }
        finally
        {
            window.Close();
        }
    }

    private static ResourceDictionary SharedWindowStylesDictionary() => new()
    {
        Source = new Uri(
            "pack://application:,,,/FloatingTools.App;component/Resources/SharedWindowStyles.xaml",
            UriKind.Absolute)
    };

    private static ResourceDictionary ColorsDictionary(AppTheme theme) => new()
    {
        Source = new Uri(
            $"pack://application:,,,/FloatingTools.App;component/SharedUi/Tokens/Colors.{theme}.xaml",
            UriKind.Absolute)
    };

    private sealed class StubThemeChangeNotifier : IWindowsThemeChangeNotifier
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
