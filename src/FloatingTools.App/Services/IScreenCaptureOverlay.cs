using System.Windows;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

internal interface IScreenCaptureOverlay
{
    Window? Owner { get; set; }

    Task<PixelRect?> SelectAsync();

    void CloseOverlay();
}
