using System.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;

namespace FloatingTools.App.Services;

/// <summary>
/// The single place theme decisions are made and applied. Resolves the
/// effective theme (System/Dark/Light) via <see cref="IAppAppearanceResolver"/>
/// and swaps the active Colors.{Dark|Light}.xaml dictionary into the supplied
/// application resources — nothing else in the app touches
/// Application.Resources.MergedDictionaries for theme purposes.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private readonly IAppAppearanceResolver _resolver;
    private readonly IWindowsThemeChangeNotifier _themeChangeNotifier;
    private readonly ResourceDictionary _applicationResources;
    private ResourceDictionary? _activeColorDictionary;
    private bool _disposed;

    public ThemeService(
        AppAppearanceMode initialMode,
        IAppAppearanceResolver resolver,
        IWindowsThemeChangeNotifier themeChangeNotifier,
        ResourceDictionary applicationResources)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _themeChangeNotifier = themeChangeNotifier
            ?? throw new ArgumentNullException(nameof(themeChangeNotifier));
        _applicationResources = applicationResources
            ?? throw new ArgumentNullException(nameof(applicationResources));

        _themeChangeNotifier.ThemeChanged += OnSystemThemeChanged;
        ApplyAppearance(initialMode);
    }

    public AppAppearanceMode CurrentMode { get; private set; }

    public AppTheme? CurrentAppliedTheme { get; private set; }

    /// <summary>Resolves and applies the effective theme for the given mode. Safe to call repeatedly.</summary>
    public void ApplyAppearance(AppAppearanceMode mode)
    {
        CurrentMode = mode;
        ApplyEffectiveTheme(_resolver.Resolve(mode));
    }

    public void StartLiveWatcher(IntPtr windowHandle) =>
        _themeChangeNotifier.Start(windowHandle);

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (CurrentMode != AppAppearanceMode.System)
        {
            return;
        }

        ApplyEffectiveTheme(_resolver.Resolve(AppAppearanceMode.System));
    }

    private void ApplyEffectiveTheme(AppTheme theme)
    {
        if (CurrentAppliedTheme == theme)
        {
            return;
        }

        var newDictionary = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/FloatingTools.App;component/SharedUi/Tokens/Colors.{theme}.xaml",
                UriKind.Absolute)
        };

        _applicationResources.MergedDictionaries.Add(newDictionary);

        if (_activeColorDictionary is not null)
        {
            _applicationResources.MergedDictionaries.Remove(_activeColorDictionary);
        }

        _activeColorDictionary = newDictionary;
        CurrentAppliedTheme = theme;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _themeChangeNotifier.ThemeChanged -= OnSystemThemeChanged;
        _themeChangeNotifier.Dispose();
    }
}
