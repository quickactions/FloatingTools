using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FloatingTools.App.Models;

namespace FloatingTools.App.Views;

public partial class ImageViewerWindow : Window
{
    private static readonly IntPtr TopMostHandle = new(-1);
    private const uint ShowWindowFlag = 0x0040;
    private readonly MonitorWorkArea _monitor;

    public ImageViewerWindow(string imagePath, MonitorWorkArea monitor)
    {
        ImagePath = imagePath;
        _monitor = monitor;
        InitializeComponent();
        DataContext = this;
        SourceInitialized += OnSourceInitialized;
    }

    public string ImagePath { get; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var bounds = _monitor.WorkArea;
        if (!SetWindowPos(
                new WindowInteropHelper(this).Handle,
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

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Backdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Close();

    private void Image_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        e.Handled = true;

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

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
