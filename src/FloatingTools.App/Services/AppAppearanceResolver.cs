using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public enum AppTheme
{
    Dark,
    Light
}

public interface IAppAppearanceResolver
{
    AppTheme Resolve(AppAppearanceMode mode);
}

public sealed class AppAppearanceResolver(
    Func<AppTheme>? currentSystemTheme = null) : IAppAppearanceResolver
{
    private readonly Func<AppTheme> _currentSystemTheme =
        currentSystemTheme ?? (() => AppTheme.Dark);

    public AppTheme Resolve(AppAppearanceMode mode) => mode switch
    {
        AppAppearanceMode.Dark => AppTheme.Dark,
        AppAppearanceMode.Light => AppTheme.Light,
        _ => _currentSystemTheme()
    };
}
