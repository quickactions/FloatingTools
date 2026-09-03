using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IScreenRegionCaptureService
{
    Task<CapturedScreenImage> CaptureAsync(
        PixelRect region,
        CancellationToken cancellationToken = default);
}
