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
    private readonly Func<MonitorWorkArea> _captureMonitor;
    private CancellationTokenSource? _activeCapture;
    private bool _shutdown;

    internal event EventHandler? CaptureStarting;
    internal event EventHandler? CaptureFinished;
    internal bool IsCapturing => _activeCapture is not null;

    internal void CancelActiveCapture() => _activeCapture?.Cancel();

    internal void StopForShutdown()
    {
        _shutdown = true;
        CancelActiveCapture();
    }

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
        Func<MonitorWorkArea, IScreenCaptureOverlay> overlayFactory,
        Func<MonitorWorkArea>? captureMonitor = null)
    {
        _placementService = placementService
            ?? throw new ArgumentNullException(nameof(placementService));
        _captureService = captureService
            ?? throw new ArgumentNullException(nameof(captureService));
        _ocrService = ocrService
            ?? throw new ArgumentNullException(nameof(ocrService));
        _overlayFactory = overlayFactory
            ?? throw new ArgumentNullException(nameof(overlayFactory));
        _captureMonitor = captureMonitor ?? (() => SelectMonitor(
            _placementService.GetCursorPosition(), _placementService.GetMonitors()));
    }

    public async Task<ScreenTextCaptureResult> CaptureTextAsync(
        CancellationToken cancellationToken = default)
    {
        var application = Application.Current;
        if (application is null)
        {
            return ScreenTextCaptureResult.Failed();
        }
        if (_shutdown || IsCapturing) return ScreenTextCaptureResult.Cancelled();
        using var captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCapture = captureCancellation;
        cancellationToken = captureCancellation.Token;

        var visibleWindows = application.Windows
            .OfType<Window>()
            // Toolbar/panel visibility is coordinated through intentional
            // minimization. Never remove the toolbar's taskbar entry via Hide.
            .Where(window => window.IsVisible && window is not ToolbarWindow && window is not PanelWindow)
            .ToArray();
        var overlayOwner = application.MainWindow ?? visibleWindows.FirstOrDefault();
        var closedWindows = new HashSet<Window>();
        void OnAuxiliaryClosed(object? sender, EventArgs e)
        {
            if (sender is Window window) closedWindows.Add(window);
        }
        foreach (var window in visibleWindows) window.Closed += OnAuxiliaryClosed;
        var suspensionStarted = false;
        var suspensionFinished = false;

        void FinishCaptureSuspension()
        {
            if (!suspensionStarted || suspensionFinished)
            {
                return;
            }

            suspensionFinished = true;
            foreach (var window in visibleWindows)
            {
                window.Closed -= OnAuxiliaryClosed;
                if (!_shutdown && !closedWindows.Contains(window) && !window.IsVisible)
                {
                    window.Show();
                }
            }

            CaptureFinished?.Invoke(this, EventArgs.Empty);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var monitor = _captureMonitor();
            suspensionStarted = true;
            CaptureStarting?.Invoke(this, EventArgs.Empty);
            foreach (var window in visibleWindows)
            {
                window.Hide();
            }

            var overlay = _overlayFactory(monitor);
            var selection = await SelectWithGuaranteedCleanupAsync(
                overlay,
                overlayOwner,
                cancellationToken);
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
            cancellationToken.ThrowIfCancellationRequested();
            FinishCaptureSuspension();
            var text = await _ocrService.RecognizeEnglishAsync(
                image,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
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
            // Preserve the previous completion ordering for cancellation or a
            // failure before a safe image buffer exists. Successful captures
            // finish their UI suspension above while OCR remains active.
            if (!suspensionFinished)
            {
                _activeCapture = null;
            }

            try
            {
                FinishCaptureSuspension();
            }
            finally
            {
                _activeCapture = null;
            }
        }
    }

    internal static async Task<PixelRect?> SelectWithGuaranteedCleanupAsync(
        IScreenCaptureOverlay overlay,
        Window? owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            overlay.Owner = owner;
            return await overlay.SelectAsync().WaitAsync(cancellationToken);
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
