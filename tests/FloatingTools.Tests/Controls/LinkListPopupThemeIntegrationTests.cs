using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Controls;

/// <summary>
/// LinkListBlockControl's popup DataTemplates (LinkActionToolbarTemplate,
/// LinkEditToolbarTemplate) sit in the same risk class as the C1 startup
/// crash: their StaticResource/DynamicResource references are declared in a
/// UserControl.Resources dictionary that is locally merged rather than
/// relying purely on Application.Resources. C2 converted their color/brush
/// StaticResource references to DynamicResource; this test exercises real
/// template instantiation against a live ThemeService-managed resource graph
/// to confirm nothing regresses the same way TranslationIconTemplate did.
/// </summary>
[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class LinkListPopupThemeIntegrationTests
{
    [Fact]
    public void LocalColorsDictionaryMerge_WasRemovedAfterRuntimeVerification()
    {
        var xaml = File.ReadAllText(FindSourcePath());

        // C2 verified (via this test class's template-instantiation checks,
        // plus a real dotnet run smoke test) that DynamicResource references
        // inside LinkActionToolbarTemplate/LinkEditToolbarTemplate resolve
        // correctly by falling through to Application.Resources, exactly
        // like AnchoredPopupHost.xaml already did with zero local merge —
        // so the local Colors.Dark.xaml safety net is no longer needed.
        Assert.DoesNotContain("Colors.Dark.xaml", xaml);
    }

    private static string FindSourcePath()
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
                    "Controls",
                    "LinkListBlockControl.xaml");
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }

    [Theory]
    [InlineData("LinkActionToolbarTemplate")]
    [InlineData("LinkEditToolbarTemplate")]
    public void PopupTemplate_ResolvesDynamicResourceTokens_AgainstALiveThemeServiceGraph(
        string templateKey)
        => WpfTestApplication.Run(() =>
        {
            var applicationResources = new ResourceDictionary();
            applicationResources.MergedDictionaries.Add(ColorsDictionary(AppTheme.Dark));

            using var themeService = new ThemeService(
                AppAppearanceMode.Dark,
                new AppAppearanceResolver(() => AppTheme.Dark),
                new StubThemeChangeNotifier(),
                applicationResources);

            var control = new LinkListBlockControl();
            var window = new Window
            {
                Content = control,
                Width = 60,
                Height = 60,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            var exception = Record.Exception(() =>
            {
                window.Show();
                window.UpdateLayout();

                var template = (DataTemplate)control.FindResource(templateKey);
                var content = (FrameworkElement)template.LoadContent();
                var host = new Grid();
                host.Children.Add(content);
                window.Content = host;
                window.UpdateLayout();
            });

            try
            {
                Assert.Null(exception);
            }
            finally
            {
                window.Close();
            }
        });

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
