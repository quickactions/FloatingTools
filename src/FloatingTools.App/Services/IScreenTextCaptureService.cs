using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IScreenTextCaptureService
{
    Task<ScreenTextCaptureResult> CaptureTextAsync(
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableScreenTextCaptureService : IScreenTextCaptureService
{
    public Task<ScreenTextCaptureResult> CaptureTextAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ScreenTextCaptureResult.Failed());
}
