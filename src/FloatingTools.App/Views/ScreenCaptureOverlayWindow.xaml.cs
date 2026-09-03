using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.Views;

public partial class ScreenCaptureOverlayWindow : Window, IScreenCaptureOverlay
{
    private readonly MonitorWorkArea _monitor;
    private readonly TaskCompletionSource<PixelRect?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PixelPoint? _selectionStart;
    private bool _completed;
    private bool _closed;

    public ScreenCaptureOverlayWindow(MonitorWorkArea monitor)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    public Task<PixelRect?> SelectAsync()
    {
        Show();
        Activate();
        Focus();
        return _completion.Task;
    }

    public void CloseOverlay()
    {
        if (!_closed)
        {
            Close();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var bounds = _monitor.WorkArea;
        var handle = new WindowInteropHelper(this).Handle;
        if (!SetWindowPos(
                handle,
                TopMostHandle,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                ShowWindowFlag))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _selectionStart = GetCursorPosition();
        CaptureMouse();
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelectionVisual(_selectionStart.Value);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelectionVisual(GetCursorPosition());
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionStart is null)
        {
            return;
        }

        ReleaseMouseCapture();
        var selection = ScreenSelectionCalculator.Create(
            _selectionStart.Value,
            GetCursorPosition(),
            _monitor.WorkArea);
        Complete(selection);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Complete(null);
        e.Handled = true;
    }

    private void UpdateSelectionVisual(PixelPoint end)
    {
        if (_selectionStart is null)
        {
            return;
        }

        var startDip = PointFromScreen(
            new Point(_selectionStart.Value.X, _selectionStart.Value.Y));
        var endDip = PointFromScreen(new Point(end.X, end.Y));
        Canvas.SetLeft(SelectionBorder, Math.Min(startDip.X, endDip.X));
        Canvas.SetTop(SelectionBorder, Math.Min(startDip.Y, endDip.Y));
        SelectionBorder.Width = Math.Abs(endDip.X - startDip.X);
        SelectionBorder.Height = Math.Abs(endDip.Y - startDip.Y);
    }

    private void Complete(PixelRect? selection)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Hide();
        _completion.TrySetResult(selection);
        CloseOverlay();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        if (!_completed)
        {
            _completed = true;
            _completion.TrySetResult(null);
        }
    }

    private static PixelPoint GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PixelPoint(point.X, point.Y);
    }

    private const uint ShowWindowFlag = 0x0040;
    private static readonly IntPtr TopMostHandle = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

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
}
