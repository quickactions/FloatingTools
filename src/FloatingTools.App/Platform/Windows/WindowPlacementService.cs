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

    public static bool RequiresConnectedPlacement(
        PixelRect toolbarBounds,
        PixelPoint toolbarPosition,
        PixelRect panelHostBounds,
        PixelPoint panelHostPosition,
        int panelHostWidth,
        int panelHostHeight) =>
        toolbarBounds.Left != toolbarPosition.X
        || toolbarBounds.Top != toolbarPosition.Y
        || panelHostBounds.Left != panelHostPosition.X
        || panelHostBounds.Top != panelHostPosition.Y
        || panelHostBounds.Width != panelHostWidth
        || panelHostBounds.Height != panelHostHeight;

    public void PlaceConnectedWindows(
        IntPtr toolbarHandle,
        PixelPoint toolbarPosition,
        IntPtr panelHandle,
        PixelPoint panelPosition,
        int panelWidth,
        int panelHeight)
    {
        if (toolbarHandle == IntPtr.Zero)
        {
            throw new ArgumentException("A toolbar window handle is required.", nameof(toolbarHandle));
        }

        if (panelHandle == IntPtr.Zero)
        {
            throw new ArgumentException("A panel window handle is required.", nameof(panelHandle));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(panelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(panelHeight);

        var deferred = BeginDeferWindowPos(2);
        if (deferred == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        deferred = DeferWindowPos(
            deferred,
            toolbarHandle,
            IntPtr.Zero,
            toolbarPosition.X,
            toolbarPosition.Y,
            0,
            0,
            SwpNoSize | SwpNoZOrder | SwpNoActivate);
        if (deferred == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        deferred = DeferWindowPos(
            deferred,
            panelHandle,
            IntPtr.Zero,
            panelPosition.X,
            panelPosition.Y,
            panelWidth,
            panelHeight,
            SwpNoZOrder | SwpNoActivate);
        if (deferred == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!EndDeferWindowPos(deferred))
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

    public WindowPlacement GetNearestDockPlacement(IntPtr windowHandle)
    {
        var bounds = GetWindowBounds(windowHandle);
        return CalculateNearestDockPlacement(bounds, GetMonitors());
    }

    public WindowPlacement DockToNearestSide(IntPtr windowHandle)
    {
        var bounds = GetWindowBounds(windowHandle);
        var monitors = GetMonitors();
        var requestedPlacement = CalculateNearestDockPlacement(bounds, monitors);
        var resolved = WindowPlacementCalculator.Resolve(
            requestedPlacement,
            monitors,
            bounds.Width,
            bounds.Height);

        MoveWindow(windowHandle, resolved.Left, resolved.Top);
        return resolved.Placement;
    }

    private static WindowPlacement CalculateNearestDockPlacement(
        PixelRect bounds,
        IReadOnlyList<MonitorWorkArea> monitors)
    {
        var monitor = WindowPlacementCalculator.SelectMonitor(bounds, monitors);
        var side = WindowPlacementCalculator.ChooseNearestDockSide(
            bounds,
            monitor.WorkArea);
        var scale = monitor.DpiScale > 0 ? monitor.DpiScale : 1;
        var requestedOffset = (bounds.Top - monitor.WorkArea.Top) / scale;
        return new WindowPlacement(monitor.MonitorId, side, requestedOffset)
        {
            HorizontalOffset = (bounds.Left - monitor.WorkArea.Left) / scale,
            TopOpeningDirection = side == DockSide.Top
                ? WindowPlacementCalculator.ChooseTopOpeningDirection(bounds, monitor.WorkArea)
                : TopOpeningDirection.Right
        };
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
    private static extern IntPtr BeginDeferWindowPos(int windowCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DeferWindowPos(
        IntPtr deferredPositionInfo,
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndDeferWindowPos(IntPtr deferredPositionInfo);

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
