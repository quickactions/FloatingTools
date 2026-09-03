using FloatingTools.App.Services;
using Microsoft.Win32;

namespace FloatingTools.App.Platform.Windows;

public interface IWindowsThemeReader
{
    AppTheme ReadCurrentTheme();
}

/// <summary>
/// Reads the per-app (not per-shell) Windows theme from the registry. Any
/// failure (missing key/value, access denied, unsupported OS) falls back to
/// Dark rather than throwing, matching this app's current all-dark design.
/// </summary>
public sealed class WindowsThemeReader : IWindowsThemeReader
{
    private const string PersonalizeKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    private readonly Func<object?> _readAppsUseLightThemeValue;

    public WindowsThemeReader()
        : this(ReadFromRegistry)
    {
    }

    internal WindowsThemeReader(Func<object?> readAppsUseLightThemeValue)
    {
        _readAppsUseLightThemeValue = readAppsUseLightThemeValue
            ?? throw new ArgumentNullException(nameof(readAppsUseLightThemeValue));
    }

    public AppTheme ReadCurrentTheme()
    {
        try
        {
            var value = _readAppsUseLightThemeValue();
            return value is int lightThemeFlag && lightThemeFlag != 0
                ? AppTheme.Light
                : AppTheme.Dark;
        }
        catch
        {
            return AppTheme.Dark;
        }
    }

    private static object? ReadFromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue(AppsUseLightThemeValueName);
    }
}
