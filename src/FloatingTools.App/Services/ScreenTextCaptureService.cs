using System.Windows;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Views;

namespace FloatingTools.App.Services;

public sealed class ScreenTextCaptureService : IScreenTextCaptureService
{
    private readonly WindowPlacementService _placementService;
    private readonly IScreenRegionCaptureService _captureService;
    private readonly ILocalOcrService _ocrService;
    private readonly Func<MonitorWorkArea, IScreenCaptureOverlay> _overlayFactory;

    public ScreenTextCaptureService(
        WindowPlacementService placementService,
        IScreenRegionCaptureService captureService,
        ILocalOcrService ocrService)
        : this(
            placementService,
            captureService,
            ocrService,
            static monitor => new ScreenCaptureOverlayWindow(monitor))
    {
    }

    internal ScreenTextCaptureService(
        WindowPlacementService placementService,
        IScreenRegionCaptureService captureService,
        ILocalOcrService ocrService,
        Func<MonitorWorkArea, IScreenCaptureOverlay> overlayFactory)
    {
        _placementService = placementService
            ?? throw new ArgumentNullException(nameof(placementService));
        _captureService = captureService
            ?? throw new ArgumentNullException(nameof(captureService));
        _ocrService = ocrService
            ?? throw new ArgumentNullException(nameof(ocrService));
        _overlayFactory = overlayFactory
            ?? throw new ArgumentNullException(nameof(overlayFactory));
    }

    public async Task<ScreenTextCaptureResult> CaptureTextAsync(
        CancellationToken cancellationToken = default)
    {
        var application = Application.Current;
        if (application is null)
        {
            return ScreenTextCaptureResult.Failed();
        }

        var visibleWindows = application.Windows
            .OfType<Window>()
            .Where(window => window.IsVisible)
            .ToArray();
        var overlayOwner = application.MainWindow ?? visibleWindows.FirstOrDefault();

        try
        {
            var cursor = _placementService.GetCursorPosition();
            var monitor = SelectMonitor(cursor, _placementService.GetMonitors());
            foreach (var window in visibleWindows)
            {
                window.Hide();
            }

            var overlay = _overlayFactory(monitor);
            var selection = await SelectWithGuaranteedCleanupAsync(
                overlay,
                overlayOwner);
            if (selection is null)
            {
                return ScreenTextCaptureResult.Cancelled();
            }

            cancellationToken.ThrowIfCancellationRequested();
            await application.Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.Render,
                cancellationToken);

            using var image = await _captureService.CaptureAsync(
                selection.Value,
                cancellationToken);
            var text = await _ocrService.RecognizeEnglishAsync(
                image,
                cancellationToken);
            return !OcrTextValidator.ContainsMeaningfulEnglishText(text)
                ? ScreenTextCaptureResult.NoText()
                : ScreenTextCaptureResult.Success(text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ScreenTextCaptureResult.Cancelled();
        }
        catch
        {
            return ScreenTextCaptureResult.Failed();
        }
        finally
        {
            foreach (var window in visibleWindows)
            {
                if (!window.IsVisible)
                {
                    window.Show();
                }
            }
        }
    }

    internal static async Task<PixelRect?> SelectWithGuaranteedCleanupAsync(
        IScreenCaptureOverlay overlay,
        Window? owner)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        try
        {
            overlay.Owner = owner;
            return await overlay.SelectAsync();
        }
        finally
        {
            overlay.CloseOverlay();
        }
    }

    private static MonitorWorkArea SelectMonitor(
        PixelPoint cursor,
        IReadOnlyList<MonitorWorkArea> monitors) =>
        monitors.FirstOrDefault(monitor =>
            cursor.X >= monitor.Bounds.Left
            && cursor.X < monitor.Bounds.Right
            && cursor.Y >= monitor.Bounds.Top
            && cursor.Y < monitor.Bounds.Bottom)
        ?? monitors.First(monitor => monitor.IsPrimary);
}
