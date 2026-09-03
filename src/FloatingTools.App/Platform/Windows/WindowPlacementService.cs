using System.ComponentModel;
using System.Runtime.InteropServices;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.Platform.Windows;

public sealed class WindowPlacementService
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    public PixelPoint GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PixelPoint(point.X, point.Y);
    }

    public PixelRect GetWindowBounds(IntPtr windowHandle)
    {
        if (!GetWindowRect(windowHandle, out var rectangle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return rectangle.ToPixelRect();
    }

    public void MoveWindow(IntPtr windowHandle, int left, int top)
    {
        if (!SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                left,
                top,
                0,
                0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public WindowPlacement Restore(IntPtr windowHandle, WindowPlacement? requestedPlacement)
    {
        var bounds = GetWindowBounds(windowHandle);
        var resolved = WindowPlacementCalculator.Resolve(
            requestedPlacement,
            GetMonitors(),
            bounds.Width,
            bounds.Height);

        MoveWindow(windowHandle, resolved.Left, resolved.Top);
        return resolved.Placement;
    }

    public WindowPlacement DockToNearestSide(IntPtr windowHandle)
    {
        var bounds = GetWindowBounds(windowHandle);
        var monitors = GetMonitors();
        var monitor = WindowPlacementCalculator.SelectMonitor(bounds, monitors);
        var side = WindowPlacementCalculator.ChooseNearestDockSide(
            bounds,
            monitor.WorkArea);
        var scale = monitor.DpiScale > 0 ? monitor.DpiScale : 1;
        var requestedOffset = (bounds.Top - monitor.WorkArea.Top) / scale;
        var requestedPlacement = new WindowPlacement(
            monitor.MonitorId,
            side,
            requestedOffset);
        var resolved = WindowPlacementCalculator.Resolve(
            requestedPlacement,
            monitors,
            bounds.Width,
            bounds.Height);

        MoveWindow(windowHandle, resolved.Left, resolved.Top);
        return resolved.Placement;
    }

    public IReadOnlyList<MonitorWorkArea> GetMonitors()
    {
        var monitors = new List<MonitorWorkArea>();

        MonitorEnumProcedure callback = (
            monitorHandle,
            _,
            _,
            _) =>
        {
            var monitorInfo = MonitorInfoEx.Create();

            if (GetMonitorInfo(monitorHandle, ref monitorInfo))
            {
                var dpiScale = GetMonitorDpiScale(monitorHandle);
                monitors.Add(new MonitorWorkArea(
                    monitorInfo.DeviceName,
                    monitorInfo.Monitor.ToPixelRect(),
                    monitorInfo.WorkArea.ToPixelRect(),
                    dpiScale,
                    (monitorInfo.Flags & 1) != 0));
            }

            return true;
        };

        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("Windows did not report an available monitor.");
        }

        return monitors;
    }

    private static double GetMonitorDpiScale(IntPtr monitorHandle)
    {
        try
        {
            var result = GetDpiForMonitor(
                monitorHandle,
                MonitorDpiType.Effective,
                out var dpiX,
                out _);

            return result == 0 && dpiX > 0
                ? dpiX / 96.0
                : 1;
        }
        catch (EntryPointNotFoundException)
        {
            return 1;
        }
        catch (DllNotFoundException)
        {
            return 1;
        }
    }

    private delegate bool MonitorEnumProcedure(
        IntPtr monitorHandle,
        IntPtr deviceContext,
        IntPtr monitorRectangle,
        IntPtr data);

    private enum MonitorDpiType
    {
        Effective = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly PixelRect ToPixelRect() => new(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRectangle Monitor;
        public NativeRectangle WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public static MonitorInfoEx Create() => new()
        {
            Size = Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRectangle,
        MonitorEnumProcedure callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitorHandle,
        ref MonitorInfoEx monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitorHandle,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);
}
