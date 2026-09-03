using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using FloatingTools.App.Services;

namespace FloatingTools.App.Platform.Windows;

public sealed class WindowsClipboardImageProvider : IClipboardImageProvider
{
    public byte[]? GetPngImage()
    {
        try
        {
            var image = Clipboard.GetImage();
            if (image is null)
            {
                return null;
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch (COMException)
        {
            return null;
        }
    }
}
