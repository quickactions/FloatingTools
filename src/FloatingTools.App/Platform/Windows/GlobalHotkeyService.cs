using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace FloatingTools.App.Platform.Windows;

/// <summary>
/// Wraps Win32 RegisterHotKey/UnregisterHotKey and routes WM_HOTKEY to
/// per-id callbacks. Native registration goes through <see cref="IGlobalHotkeyNativeApi"/>
/// so ordinary unit tests can exercise registration/conflict/dispose logic
/// without ever claiming a real system-wide hotkey.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly IGlobalHotkeyNativeApi _nativeApi;
    private readonly Dictionary<int, Action> _callbacksById = [];
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _disposed;

    public GlobalHotkeyService()
        : this(new Win32GlobalHotkeyNativeApi())
    {
    }

    internal GlobalHotkeyService(IGlobalHotkeyNativeApi nativeApi)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
    }

    /// <summary>
    /// Attempts to register a global hotkey. Returns false (never throws) if the
    /// combination is already claimed by this or another process, or if this
    /// service has already registered that id.
    /// </summary>
    public bool TryRegister(
        IntPtr windowHandle,
        int id,
        ModifierKeys modifiers,
        Key key,
        Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_disposed || _callbacksById.ContainsKey(id))
        {
            return false;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!_nativeApi.RegisterHotKey(windowHandle, id, (uint)modifiers | ModNoRepeat, virtualKey))
        {
            return false;
        }

        EnsureHooked(windowHandle);
        _windowHandle = windowHandle;
        _callbacksById[id] = callback;
        return true;
    }

    /// <summary>
    /// Exposed for tests: dispatches a synthesized WM_HOTKEY message the same
    /// way the real window message pump would, without needing a real HWND.
    /// </summary>
    internal bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg != WmHotKey || !_callbacksById.TryGetValue(wParam.ToInt32(), out var callback))
        {
            return false;
        }

        callback();
        return true;
    }

    private void EnsureHooked(IntPtr windowHandle)
    {
        if (_hwndSource is not null)
        {
            return;
        }

        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = HandleMessage(msg, wParam);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var id in _callbacksById.Keys)
        {
            _nativeApi.UnregisterHotKey(_windowHandle, id);
        }

        _callbacksById.Clear();

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }
}

internal interface IGlobalHotkeyNativeApi
{
    bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    bool UnregisterHotKey(IntPtr windowHandle, int id);
}

internal sealed class Win32GlobalHotkeyNativeApi : IGlobalHotkeyNativeApi
{
    public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey) =>
        NativeMethods.RegisterHotKey(windowHandle, id, modifiers, virtualKey);

    public bool UnregisterHotKey(IntPtr windowHandle, int id) =>
        NativeMethods.UnregisterHotKey(windowHandle, id);

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(
            IntPtr hWnd,
            int id,
            uint fsModifiers,
            uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
