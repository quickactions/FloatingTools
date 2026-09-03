namespace FloatingTools.App.Services;

public interface IClipboardImageProvider
{
    byte[]? GetPngImage();
}

public sealed class NullClipboardImageProvider : IClipboardImageProvider
{
    public byte[]? GetPngImage() => null;
}
