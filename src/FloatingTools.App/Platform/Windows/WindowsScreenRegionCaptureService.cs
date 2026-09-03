using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.Platform.Windows;

public sealed class WindowsScreenRegionCaptureService : IScreenRegionCaptureService
{
    public Task<CapturedScreenImage> CaptureAsync(
        PixelRect region,
        CancellationToken cancellationToken = default)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region));
        }

        return Task.Run(
            () => Capture(region, cancellationToken),
            cancellationToken);
    }

    private static CapturedScreenImage Capture(
        PixelRect region,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previousObject = IntPtr.Zero;

        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            bitmap = CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (bitmap == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            previousObject = SelectObject(memoryDc, bitmap);
            if (previousObject == IntPtr.Zero || previousObject == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!BitBlt(
                    memoryDc,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    screenDc,
                    region.Left,
                    region.Top,
                    SourceCopy | CaptureBlt))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            cancellationToken.ThrowIfCancellationRequested();
            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();

            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return new CapturedScreenImage(stream.ToArray());
        }
        finally
        {
            if (previousObject != IntPtr.Zero
                && previousObject != new IntPtr(-1)
                && memoryDc != IntPtr.Zero)
            {
                SelectObject(memoryDc, previousObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private const int SourceCopy = 0x00CC0020;
    private const int CaptureBlt = 0x40000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleBitmap(
        IntPtr deviceContext,
        int width,
        int height);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr value);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        IntPtr destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        int operation);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr deviceContext);
}
