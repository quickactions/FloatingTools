using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FloatingTools.App.Platform.Windows;

public interface IWindowsThemeChangeNotifier : IDisposable
{
    event EventHandler? ThemeChanged;

    void Start(IntPtr windowHandle);
}

/// <summary>
/// Watches for live Windows app-theme changes via WM_SETTINGCHANGE with
/// lParam "ImmersiveColorSet" — the precise broadcast Explorer sends when only
/// the immersive/app theme changes, routed through HwndSource.AddHook exactly
/// like GlobalHotkeyService's WM_HOTKEY handling.
/// </summary>
public sealed class WindowsThemeWatcher : IWindowsThemeChangeNotifier
{
    private const int WmSettingChange = 0x001A;
    private const string ImmersiveColorSetSettingName = "ImmersiveColorSet";

    private HwndSource? _hwndSource;
    private bool _disposed;

    public event EventHandler? ThemeChanged;

    public void Start(IntPtr windowHandle)
    {
        if (_disposed || _hwndSource is not null || windowHandle == IntPtr.Zero)
        {
            return;
        }

        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    /// <summary>
    /// Exposed for tests: evaluates a message the same way the real window
    /// message pump would, without needing a real HWND or OS theme change.
    /// </summary>
    internal bool HandleMessage(int msg, IntPtr lParam)
    {
        if (msg != WmSettingChange || lParam == IntPtr.Zero)
        {
            return false;
        }

        var settingName = Marshal.PtrToStringUni(lParam);
        if (!string.Equals(settingName, ImmersiveColorSetSettingName, StringComparison.Ordinal))
        {
            return false;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = HandleMessage(msg, lParam);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }
}
