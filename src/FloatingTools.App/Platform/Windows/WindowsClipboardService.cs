using System.Windows;
using FloatingTools.App.Services;

namespace FloatingTools.App.Platform.Windows;

public sealed class WindowsClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Clipboard.SetText(text);
    }
}
