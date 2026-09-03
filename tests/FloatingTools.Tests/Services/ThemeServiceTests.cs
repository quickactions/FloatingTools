using System.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class ThemeServiceTests
{
    [Fact]
    public void Constructing_WithDarkMode_AppliesTheDarkDictionary()
        => WpfTestApplication.Run(() =>
        {
            var resources = new ResourceDictionary();
            using var service = CreateService(AppAppearanceMode.Dark, resources: resources);

            Assert.Equal(AppTheme.Dark, service.CurrentAppliedTheme);
            Assert.Single(resources.MergedDictionaries);
            Assert.Contains("Colors.Dark.xaml", ActiveSourceUri(resources));
        });

    [Fact]
    public void Constructing_WithLightMode_AppliesTheLightDictionary()
        => WpfTestApplication.Run(() =>
        {
            var resources = new ResourceDictionary();
            using var service = CreateService(AppAppearanceMode.Light, resources: resources);

            Assert.Equal(AppTheme.Light, service.CurrentAppliedTheme);
            Assert.Single(resources.MergedDictionaries);
            Assert.Contains("Colors.Light.xaml", ActiveSourceUri(resources));
        });

    [Fact]
    public void Constructing_WithSystemMode_AppliesTheResolvedSystemTheme()
        => WpfTestApplication.Run(() =>
        {
            var resolver = new FakeAppAppearanceResolver { SystemTheme = AppTheme.Light };
            var resources = new ResourceDictionary();
            using var service = CreateService(
                AppAppearanceMode.System, resolver: resolver, resources: resources);

            Assert.Equal(AppTheme.Light, service.CurrentAppliedTheme);
        });

    [Fact]
    public void ExplicitDarkOrLight_IgnoresLiveSystemThemeChangeNotifications()
        => WpfTestApplication.Run(() =>
        {
            var notifier = new FakeThemeChangeNotifier();
            var resolver = new FakeAppAppearanceResolver { SystemTheme = AppTheme.Light };
            using var service = CreateService(
                AppAppearanceMode.Dark, resolver: resolver, notifier: notifier);

            notifier.RaiseThemeChanged();

            Assert.Equal(AppTheme.Dark, service.CurrentAppliedTheme);
        });

    [Fact]
    public void SystemMode_ReactsToLiveSystemThemeChangeNotifications()
        => WpfTestApplication.Run(() =>
        {
            var notifier = new FakeThemeChangeNotifier();
            var resolver = new FakeAppAppearanceResolver { SystemTheme = AppTheme.Dark };
            using var service = CreateService(
                AppAppearanceMode.System, resolver: resolver, notifier: notifier);
            Assert.Equal(AppTheme.Dark, service.CurrentAppliedTheme);

            resolver.SystemTheme = AppTheme.Light;
            notifier.RaiseThemeChanged();

            Assert.Equal(AppTheme.Light, service.CurrentAppliedTheme);
        });

    [Fact]
    public void SwitchingBackToSystem_ImmediatelyResolvesTheCurrentSystemState()
        => WpfTestApplication.Run(() =>
        {
            var resolver = new FakeAppAppearanceResolver { SystemTheme = AppTheme.Light };
            using var service = CreateService(AppAppearanceMode.Dark, resolver: resolver);
            Assert.Equal(AppTheme.Dark, service.CurrentAppliedTheme);

            service.ApplyAppearance(AppAppearanceMode.System);

            Assert.Equal(AppTheme.Light, service.CurrentAppliedTheme);
        });

    [Fact]
    public void RepeatedApplyAppearance_NeverLeavesMoreThanOneColorDictionaryMerged()
        => WpfTestApplication.Run(() =>
        {
            var resources = new ResourceDictionary();
            using var service = CreateService(AppAppearanceMode.Dark, resources: resources);

            service.ApplyAppearance(AppAppearanceMode.Light);
            service.ApplyAppearance(AppAppearanceMode.Light);
            service.ApplyAppearance(AppAppearanceMode.Dark);
            service.ApplyAppearance(AppAppearanceMode.Dark);
            service.ApplyAppearance(AppAppearanceMode.System);

            Assert.Single(resources.MergedDictionaries);
        });

    [Fact]
    public void StartLiveWatcher_DelegatesToTheNotifier()
        => WpfTestApplication.Run(() =>
        {
            var notifier = new FakeThemeChangeNotifier();
            using var service = CreateService(AppAppearanceMode.Dark, notifier: notifier);
            var handle = new IntPtr(42);

            service.StartLiveWatcher(handle);

            Assert.Equal([handle], notifier.StartCalls);
        });

    [Fact]
    public void Dispose_UnhooksTheNotifierAndIsIdempotent()
        => WpfTestApplication.Run(() =>
        {
            var notifier = new FakeThemeChangeNotifier();
            var service = CreateService(AppAppearanceMode.Dark, notifier: notifier);

            service.Dispose();
            service.Dispose();

            // Idempotent: the second Dispose() call must be a safe no-op, not
            // a second pass-through to the notifier.
            Assert.Equal(1, notifier.DisposeCallCount);
        });

    [Fact]
    public void Dispose_ThenSystemThemeChange_NoLongerAffectsTheAppliedTheme()
        => WpfTestApplication.Run(() =>
        {
            var notifier = new FakeThemeChangeNotifier();
            var resolver = new FakeAppAppearanceResolver { SystemTheme = AppTheme.Dark };
            var service = CreateService(
                AppAppearanceMode.System, resolver: resolver, notifier: notifier);

            service.Dispose();
            resolver.SystemTheme = AppTheme.Light;
            notifier.RaiseThemeChanged();

            Assert.Equal(AppTheme.Dark, service.CurrentAppliedTheme);
        });

    private static ThemeService CreateService(
        AppAppearanceMode initialMode,
        FakeAppAppearanceResolver? resolver = null,
        FakeThemeChangeNotifier? notifier = null,
        ResourceDictionary? resources = null) =>
        new(
            initialMode,
            resolver ?? new FakeAppAppearanceResolver(),
            notifier ?? new FakeThemeChangeNotifier(),
            resources ?? new ResourceDictionary());

    private static string ActiveSourceUri(ResourceDictionary resources) =>
        resources.MergedDictionaries.Single().Source!.ToString();

    private sealed class FakeThemeChangeNotifier : IWindowsThemeChangeNotifier
    {
        public event EventHandler? ThemeChanged;

        public List<IntPtr> StartCalls { get; } = [];

        public int DisposeCallCount { get; private set; }

        public void Start(IntPtr windowHandle) => StartCalls.Add(windowHandle);

        public void RaiseThemeChanged() => ThemeChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() => DisposeCallCount++;
    }

    private sealed class FakeAppAppearanceResolver : IAppAppearanceResolver
    {
        public AppTheme SystemTheme { get; set; } = AppTheme.Dark;

        public AppTheme Resolve(AppAppearanceMode mode) => mode switch
        {
            AppAppearanceMode.Dark => AppTheme.Dark,
            AppAppearanceMode.Light => AppTheme.Light,
            _ => SystemTheme
        };
    }
}
