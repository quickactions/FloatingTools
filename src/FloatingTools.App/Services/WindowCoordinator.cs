using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.App.Services;

public sealed class WindowCoordinator
{
    internal const int ShowHideHotkeyId = 1;
    internal const int ExtractTextHotkeyId = 2;
    internal const int CaptureOnlyHotkeyId = 3;

    private readonly FloatingToolbarViewModel _viewModel;
    private readonly WindowPlacementService _placementService;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly NotesToolViewModel _notesToolViewModel;
    private readonly IActiveQuickChatConversation _activeQuickChatConversation;
    private readonly QuickChatViewModel _quickChatViewModel;
    private readonly CalendarToolViewModel _calendarToolViewModel;
    private readonly TranslationToolViewModel _translationToolViewModel;
    private readonly GlobalHotkeyService _globalHotkeyService;
    private readonly ThemeService _themeService;
    private readonly ExitWorkflow _exitWorkflow;
    private readonly PanelDragVisibilitySession _panelDragSession = new();
    private readonly ApplicationVisibilitySession _visibilitySession = new();
    private bool _isClosing;
    private bool _globalHotkeysRegistered;
    private bool _themeWatcherStarted;
    private ToolId _observedTool;
    private readonly ScreenTextCaptureService? _screenTextCaptureService;
    private bool _captureSuspended;
    private bool _capturePanelWasVisible;
    private WindowState _captureToolbarState;
    private bool _restoreAfterCapture;
    private bool _restoreCapturePanelOnNormal;
    private bool _exitRequested;
    private string? _pendingCaptureStatus;
    private bool _openTranslationAfterHiddenCapture;
    private bool IsStopping => _isClosing || _exitRequested;

    public WindowCoordinator(
        ToolbarWindow toolbarWindow,
        PanelWindow panelWindow,
        FloatingToolbarViewModel viewModel,
        WindowPlacementService placementService,
        SettingsService settingsService,
        AppSettings settings,
        NotesToolViewModel notesToolViewModel,
        IActiveQuickChatConversation activeQuickChatConversation,
        QuickChatViewModel quickChatViewModel,
        CalendarToolViewModel calendarToolViewModel,
        TranslationToolViewModel translationToolViewModel,
        GlobalHotkeyService globalHotkeyService,
        ThemeService themeService,
        ScreenTextCaptureService? screenTextCaptureService = null)
    {
        ToolbarWindow = toolbarWindow
            ?? throw new ArgumentNullException(nameof(toolbarWindow));
        PanelWindow = panelWindow
            ?? throw new ArgumentNullException(nameof(panelWindow));
        _viewModel = viewModel
            ?? throw new ArgumentNullException(nameof(viewModel));
        _placementService = placementService
            ?? throw new ArgumentNullException(nameof(placementService));
        _settingsService = settingsService
            ?? throw new ArgumentNullException(nameof(settingsService));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _notesToolViewModel = notesToolViewModel
            ?? throw new ArgumentNullException(nameof(notesToolViewModel));
        _activeQuickChatConversation = activeQuickChatConversation
            ?? throw new ArgumentNullException(nameof(activeQuickChatConversation));
        _quickChatViewModel = quickChatViewModel
            ?? throw new ArgumentNullException(nameof(quickChatViewModel));
        _calendarToolViewModel = calendarToolViewModel
            ?? throw new ArgumentNullException(nameof(calendarToolViewModel));
        _translationToolViewModel = translationToolViewModel
            ?? throw new ArgumentNullException(nameof(translationToolViewModel));
        _globalHotkeyService = globalHotkeyService
            ?? throw new ArgumentNullException(nameof(globalHotkeyService));
        _themeService = themeService
            ?? throw new ArgumentNullException(nameof(themeService));
        _screenTextCaptureService = screenTextCaptureService;
        if (_screenTextCaptureService is not null)
        {
            _screenTextCaptureService.CaptureStarting += OnCaptureStarting;
            _screenTextCaptureService.CaptureFinished += OnCaptureFinished;
        }
        _exitWorkflow = new ExitWorkflow(
            [
                new("Notes preparation", () =>
                    _notesToolViewModel.PrepareForExitAsync()),
                new("Quick Chat preparation", () =>
                    _activeQuickChatConversation.PrepareForExitAsync()),
                new("Quick Chat view disposal", () =>
                    _quickChatViewModel.DisposeAsync().AsTask()),
                new("Calendar preparation", () =>
                    _calendarToolViewModel.PrepareForExitAsync()),
                new("Global hotkey cleanup", () =>
                {
                    _globalHotkeyService.Dispose();
                    return Task.CompletedTask;
                }),
                new("Theme watcher cleanup", () =>
                {
                    _themeService.Dispose();
                    return Task.CompletedTask;
                })
            ],
            CloseAll,
            static () => Application.Current?.Shutdown(),
            ReportExitFailure);
        _observedTool = _viewModel.LastUsedTool;
        ToolbarWindow.DataContext = _viewModel;

        ToolbarWindow.TranslationRequested += OnTranslationRequested;
        ToolbarWindow.ToolMenuRequested += OnToolMenuRequested;
        ToolbarWindow.SettingsRequested += OnSettingsRequested;
        ToolbarWindow.ExitRequested += OnExitRequested;
        ToolbarWindow.DragStarted += OnToolbarDragStarted;
        ToolbarWindow.DragCompleted += OnToolbarDragCompleted;
        ToolbarWindow.Closing += OnToolbarWindowClosing;
        ToolbarWindow.Closed += OnToolbarWindowClosed;
        ToolbarWindow.NormalPlacementReady += OnToolbarNormalPlacementReady;
        ToolbarWindow.CaptureRestoreRequested += OnCaptureRestoreRequested;
        PanelWindow.CloseRequested += OnPanelCloseRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public ToolbarWindow ToolbarWindow { get; }

    public PanelWindow PanelWindow { get; }

    public void ShowToolbar()
    {
        if (IsStopping || _visibilitySession.IsHidden || _captureSuspended) return;
        if (!ToolbarWindow.IsVisible)
        {
            ToolbarWindow.Show();
        }
        ToolbarWindow.RestoreUi();

        EnsureOwnership();
        EnsureGlobalHotkeysRegistered();
        EnsureThemeWatcherStarted();
    }

    public void ShowPanel()
    {
        if (_viewModel.PanelState == PanelState.Closed
            || _visibilitySession.IsHidden
            || _captureSuspended
            || _panelDragSession.IsDragActive
            || IsStopping)
        {
            return;
        }

        ShowToolbar();
        if (!ToolbarWindow.CanUseNormalPlacement) return;
        EnsureOwnership();
        AlignPanelToToolbar();
        ToolbarWindow.UpdatePanelConnection(isPanelConnected: true);

        if (!PanelWindow.IsVisible)
        {
            PanelWindow.Show();
        }
    }

    public void HidePanel()
    {
        _panelDragSession.PanelClosed();
        ToolbarWindow.UpdatePanelConnection(isPanelConnected: false);

        if (PanelWindow.IsVisible)
        {
            PanelWindow.Hide();
        }
    }

    public void CloseAll()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        ToolbarWindow.StopUiRestoration();
        _screenTextCaptureService?.StopForShutdown();
        _panelDragSession.Cancel();
        Unsubscribe();
        CloseWindow(PanelWindow, "panel window close");
        CloseWindow(ToolbarWindow, "toolbar window close");
    }

    private void AlignPanelToToolbar()
    {
        if (!ToolbarWindow.CanUseNormalPlacement || _visibilitySession.IsHidden || IsStopping || _captureSuspended) return;
        var toolbarHandle = new WindowInteropHelper(ToolbarWindow).Handle;
        var toolbarBounds = _placementService.GetWindowBounds(toolbarHandle);
        var monitors = _placementService.GetMonitors();
        var monitor = WindowPlacementCalculator.SelectMonitor(
            toolbarBounds,
            monitors);
        var dockSide = _settings.WindowPlacement?.DockSide
            ?? WindowPlacementCalculator.ChooseNearestDockSide(
                toolbarBounds,
                monitor.WorkArea);
        var scale = double.IsFinite(monitor.DpiScale) && monitor.DpiScale > 0
            ? monitor.DpiScale
            : 1;

        var availableWidthDip = monitor.WorkArea.Width / scale;
        var requestedWidthDip = _viewModel.PanelState == PanelState.ToolMenu
            ? PanelSizeCalculator.ToolMenuWidth
            : PanelSizeCalculator.GetRequestedActiveToolSize(
                _viewModel.ActiveToolPanelSize).Width;
        var maximumPanelHeightDip = Math.Max(
            0,
            (monitor.WorkArea.Height - toolbarBounds.Height) / scale);
        var requiredPanelHeightPixels = _viewModel.PanelState is
            PanelState.ActiveTool or PanelState.ApplicationSettings
            ? Math.Min(
                Math.Max(0, monitor.WorkArea.Height - toolbarBounds.Height),
                Math.Max(
                    0,
                    (int)Math.Round(
                        PanelSizeCalculator.GetRequiredActiveToolHeight(
                            _viewModel.ActiveToolPanelSize,
                            maximumPanelHeightDip) * scale)))
            : 0;
        var provisionalWidthPixels = Math.Max(
            1,
            (int)Math.Round(
                Math.Min(requestedWidthDip, availableWidthDip) * scale));
        var placement = PanelWindowPlacementCalculator.Calculate(
            toolbarBounds,
            provisionalWidthPixels,
            monitor,
            dockSide,
            requiredPanelHeightPixels);
        var availableHeightDip = Math.Max(
            0,
            (monitor.WorkArea.Bottom - placement.PanelPosition.Y) / scale);

        PanelWindow.ApplyLayout(
            _viewModel.PanelState,
            _viewModel.ActiveToolPanelSize,
            availableWidthDip,
            availableHeightDip,
            dockSide);
        PanelWindow.UpdateLayout();

        var panelWidthPixels = Math.Max(
            1,
            (int)Math.Round(PanelWindow.Width * scale));
        placement = PanelWindowPlacementCalculator.Calculate(
            toolbarBounds,
            panelWidthPixels,
            monitor,
            dockSide,
            requiredPanelHeightPixels);

        _placementService.MoveWindow(
            toolbarHandle,
            placement.ToolbarPosition.X,
            placement.ToolbarPosition.Y);

        var panelHandle = new WindowInteropHelper(PanelWindow).EnsureHandle();
        _placementService.MoveWindow(
            panelHandle,
            placement.PanelPosition.X,
            placement.PanelPosition.Y);

        _settings.WindowPlacement = new WindowPlacement(
            monitor.MonitorId,
            dockSide,
            (placement.ToolbarPosition.Y - monitor.WorkArea.Top) / scale);
        _settingsService.Save(_settings);
    }

    private void EnsureOwnership()
    {
        if (PanelWindow.Owner is null)
        {
            PanelWindow.Owner = ToolbarWindow;
        }
    }

    private void EnsureGlobalHotkeysRegistered()
    {
        if (_globalHotkeysRegistered)
        {
            return;
        }

        var handle = new WindowInteropHelper(ToolbarWindow).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _globalHotkeysRegistered = true;

        var showHideRegistered = _globalHotkeyService.TryRegister(
            handle,
            ShowHideHotkeyId,
            ModifierKeys.Control | ModifierKeys.Alt,
            Key.H,
            ToggleApplicationVisibility);
        var captureTranslateRegistered = _globalHotkeyService.TryRegister(
            handle,
            ExtractTextHotkeyId,
            ModifierKeys.Control | ModifierKeys.Alt,
            Key.T,
            TriggerCaptureAndTranslateFromScreen);
        var captureOnlyRegistered = _globalHotkeyService.TryRegister(
            handle,
            CaptureOnlyHotkeyId,
            ModifierKeys.Control | ModifierKeys.Alt,
            Key.C,
            TriggerExtractTextFromScreen);

        UpdateGlobalShortcutsStatus(
            showHideRegistered,
            captureTranslateRegistered,
            captureOnlyRegistered);
    }

    private void EnsureThemeWatcherStarted()
    {
        if (_themeWatcherStarted)
        {
            return;
        }

        var handle = new WindowInteropHelper(ToolbarWindow).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _themeWatcherStarted = true;
        _themeService.StartLiveWatcher(handle);
    }

    private void UpdateGlobalShortcutsStatus(
        bool showHideRegistered,
        bool captureTranslateRegistered,
        bool captureOnlyRegistered)
    {
        if (showHideRegistered && captureTranslateRegistered && captureOnlyRegistered)
        {
            _translationToolViewModel.Settings.GlobalShortcutsStatusMessage = null;
            return;
        }

        var unavailable = new List<string>();
        if (!showHideRegistered)
        {
            unavailable.Add("Show/Hide (Ctrl+Alt+H)");
        }

        if (!captureTranslateRegistered)
        {
            unavailable.Add("Capture & Translate (Ctrl+Alt+T)");
        }

        if (!captureOnlyRegistered)
        {
            unavailable.Add("Capture Text (Ctrl+Alt+C)");
        }

        var message =
            $"{string.Join(" and ", unavailable)} unavailable — already in use by another app.";
        _translationToolViewModel.Settings.GlobalShortcutsStatusMessage = message;
        Trace.TraceWarning($"FloatingTools global hotkey registration failed: {message}");
    }

    private void ToggleApplicationVisibility()
    {
        if (IsStopping)
        {
            return;
        }

        if (_visibilitySession.IsHidden)
        {
            RestoreApplicationVisibility();
        }
        else
        {
            HideApplicationVisibility();
        }
    }

    private void HideApplicationVisibility()
    {
        _restoreAfterCapture = false;
        _restoreCapturePanelOnNormal = false;
        _openTranslationAfterHiddenCapture = false;
        if (_captureSuspended)
        {
            _visibilitySession.Hide(_capturePanelWasVisible);
            return;
        }
        ToolbarWindow.CompletePendingDrag();
        if (!_visibilitySession.Hide(PanelWindow.IsVisible))
        {
            return;
        }

        ToolbarWindow.MinimizeUi();
    }

    private void RestoreApplicationVisibility()
    {
        if (IsStopping) return;
        if (_captureSuspended)
        {
            _restoreAfterCapture = true;
            _screenTextCaptureService?.CancelActiveCapture();
            return;
        }
        if (!ToolbarWindow.CanUseNormalPlacement)
        {
            ToolbarWindow.RestoreUi();
            return;
        }
        if (!_visibilitySession.Show())
        {
            return;
        }

        var openCapturedTranslation = _openTranslationAfterHiddenCapture;
        _openTranslationAfterHiddenCapture = false;
        if (_visibilitySession.PanelWasVisibleBeforeHide || openCapturedTranslation)
        {
            ShowPanel();
        }
        else
        {
            if (PanelWindow.IsVisible) PanelWindow.Hide();
            ShowToolbar();
        }
        ToolbarWindow.Activate();
        TryShowPendingCaptureStatus();
    }

    private void OnToolbarNormalPlacementReady(object? sender, EventArgs e)
    {
        if (IsStopping || _captureSuspended) return;
        if (_visibilitySession.IsHidden) RestoreApplicationVisibility();
        else if (_restoreCapturePanelOnNormal)
        {
            _restoreCapturePanelOnNormal = false;
            if (_capturePanelWasVisible) ShowPanel(); else HidePanel();
        }
        else if (_viewModel.PanelState != PanelState.Closed) ShowPanel();
        TryShowPendingCaptureStatus();
    }

    private void OnCaptureStarting(object? sender, EventArgs e)
    {
        if (IsStopping) throw new OperationCanceledException();
        ToolbarWindow.CompletePendingDrag();
        _capturePanelWasVisible = PanelWindow.IsVisible && !_visibilitySession.IsHidden;
        _captureToolbarState = ToolbarWindow.WindowState;
        _restoreAfterCapture = false;
        _pendingCaptureStatus = null;
        _openTranslationAfterHiddenCapture = false;
        _captureSuspended = true;
        ToolbarWindow.DeferRestoreForCapture = true;
        ToolbarWindow.MinimizeUi();
    }

    private void OnCaptureRestoreRequested(object? sender, EventArgs e) => RestoreApplicationVisibility();

    private void OnCaptureFinished(object? sender, CaptureFinishedEventArgs e)
    {
        // A native restore may be queued just as capture completes. Preserve
        // that intent even if its dispatcher callback has not run yet.
        _restoreAfterCapture |= ToolbarWindow.TakePendingCaptureRestore();
        _captureSuspended = false;
        ToolbarWindow.DeferRestoreForCapture = false;
        if (IsStopping) return;
        if (_restoreAfterCapture && _visibilitySession.IsHidden)
        {
            RestoreApplicationVisibility();
            return;
        }
        if (_visibilitySession.IsHidden)
        {
            if (e.SelectionCompleted) RestoreApplicationVisibility();
            return;
        }
        if (!_restoreAfterCapture && _captureToolbarState == WindowState.Minimized) return;
        _restoreCapturePanelOnNormal = true;
        if (ToolbarWindow.CanUseNormalPlacement) OnToolbarNormalPlacementReady(this, EventArgs.Empty);
        else ToolbarWindow.RestoreUi();
    }

    private async void TriggerExtractTextFromScreen()
    {
        await CaptureFromHotkeyAsync(translate: false);
    }

    private async void TriggerCaptureAndTranslateFromScreen()
    {
        await CaptureFromHotkeyAsync(translate: true);
    }

    private async Task CaptureFromHotkeyAsync(bool translate)
    {
        if (IsStopping || _translationToolViewModel.IsCapturingText)
        {
            return;
        }

        var wasHidden = _visibilitySession.IsHidden;
        var outcome = await _translationToolViewModel.CaptureTextForShortcutAsync();
        if (IsStopping || !outcome.Started)
        {
            return;
        }

        if (outcome.AppliedText is null)
        {
            if (outcome.SelectionCompleted && (wasHidden || !_visibilitySession.IsHidden))
            {
                if (outcome.Status is ScreenTextCaptureStatus.NoText
                    || outcome.Status is ScreenTextCaptureStatus.Success)
                {
                    if (wasHidden || !_capturePanelWasVisible)
                        ShowCaptureStatus("No text detected.", wasHidden);
                }
                else if (outcome.Status == ScreenTextCaptureStatus.Failed
                    && (wasHidden || !PanelWindow.IsVisible
                        || _viewModel.ActiveTool != ToolId.Translation))
                {
                    ShowCaptureStatus(
                        "Could not read text from the selected area.", wasHidden);
                }
            }

            return;
        }

        _viewModel.SelectToolCommand.Execute(ToolId.Translation);
        if (_visibilitySession.IsHidden)
        {
            if (outcome.SelectionCompleted && wasHidden)
                _openTranslationAfterHiddenCapture = true;
        }
        else
        {
            _capturePanelWasVisible = true;
            if (!_restoreCapturePanelOnNormal)
            {
                ShowPanel();
            }
        }

        if (!translate || IsStopping)
        {
            return;
        }

        // Send snapshots InputText before its first await. Restore this invocation's
        // OCR text if an earlier translation or user edit changed the composer.
        _translationToolViewModel.InputText = outcome.AppliedText;
        if (_translationToolViewModel.SendCommand.CanExecute(null))
        {
            await _translationToolViewModel.SendCommand.ExecuteAsync(null);
        }
    }

    private void ShowCaptureStatus(string message, bool afterHiddenRestore)
    {
        if (!afterHiddenRestore)
        {
            ToolbarWindow.ShowTransientStatus(message);
            return;
        }

        // A minimized toolbar dismisses status during restoration. Show the
        // pending result after its existing normal-placement path finishes.
        _pendingCaptureStatus = message;
        TryShowPendingCaptureStatus();
    }

    private void TryShowPendingCaptureStatus()
    {
        if (_pendingCaptureStatus is null || IsStopping || _captureSuspended
            || _visibilitySession.IsHidden || !ToolbarWindow.CanUseNormalPlacement)
        {
            return;
        }

        var message = _pendingCaptureStatus;
        _pendingCaptureStatus = null;
        ToolbarWindow.ShowTransientStatus(message);
    }

    private void OnTranslationRequested(object? sender, EventArgs e)
    {
        _viewModel.ToggleActiveToolPanelCommand.Execute(null);
    }

    private void OnToolMenuRequested(object? sender, EventArgs e)
    {
        _viewModel.ToggleToolMenuCommand.Execute(null);
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        if (_viewModel.PanelState == PanelState.ActiveTool
            && _viewModel.ActiveTool == ToolId.Notes)
        {
            await _notesToolViewModel.LeaveAsync();
        }

        _viewModel.OpenApplicationSettingsCommand.Execute(null);
        ShowPanel();
    }

    private async void OnExitRequested(object? sender, EventArgs e)
    {
        await RequestExitAsync();
    }

    private async void OnPanelCloseRequested(object? sender, EventArgs e)
    {
        if (_viewModel.PanelState == PanelState.ActiveTool
            && _viewModel.ActiveTool == ToolId.Notes)
        {
            await _notesToolViewModel.LeaveAsync();
        }

        _viewModel.ClosePanelCommand.Execute(null);
    }

    private void OnToolbarDragStarted(object? sender, EventArgs e)
    {
        if (_isClosing
            || !_panelDragSession.Begin(_viewModel.PanelState))
        {
            return;
        }

        ToolbarWindow.UpdatePanelConnection(isPanelConnected: false);

        if (PanelWindow.IsVisible)
        {
            PanelWindow.Hide();
        }
    }

    private void OnToolbarDragCompleted(object? sender, EventArgs e)
    {
        var shouldRestore = _panelDragSession.Complete(
            _viewModel.PanelState,
            _isClosing);

        if (shouldRestore)
        {
            ShowPanel();
        }
    }

    private async void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FloatingToolbarViewModel.LastUsedTool))
        {
            if (_observedTool == ToolId.Notes
                && _viewModel.LastUsedTool != ToolId.Notes)
            {
                await _notesToolViewModel.LeaveAsync();
            }

            _observedTool = _viewModel.LastUsedTool;
            _settings.LastUsedTool = _viewModel.LastUsedTool;
            _settingsService.Save(_settings);

            if (_viewModel.PanelState == PanelState.ActiveTool
                && !_panelDragSession.IsDragActive)
            {
                ShowPanel();
            }

            return;
        }

        if (e.PropertyName == nameof(FloatingToolbarViewModel.ActiveToolPanelSize))
        {
            _settings.ActiveToolPanelSize = _viewModel.ActiveToolPanelSize;

            if (_viewModel.PanelState is
                    PanelState.ActiveTool or PanelState.ApplicationSettings
                && !_panelDragSession.IsDragActive)
            {
                ShowPanel();
            }
            else
            {
                _settingsService.Save(_settings);
            }

            return;
        }

        if (e.PropertyName != nameof(FloatingToolbarViewModel.PanelState))
        {
            return;
        }

        if (_viewModel.PanelState == PanelState.Closed)
        {
            if (_viewModel.ActiveTool == ToolId.Notes)
            {
                await _notesToolViewModel.LeaveAsync();
            }

            HidePanel();
        }
        else if (_panelDragSession.IsDragActive)
        {
            return;
        }
        else
        {
            ShowPanel();
        }
    }

    private async void OnToolbarWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        e.Cancel = true;
        await RequestExitAsync();
    }

    internal Task RequestExitAsync()
    {
        _exitRequested = true;
        ToolbarWindow.StopUiRestoration();
        _screenTextCaptureService?.StopForShutdown();
        return _exitWorkflow.ExecuteAsync();
    }

    private void OnToolbarWindowClosed(object? sender, EventArgs e)
    {
        if (!_isClosing)
        {
            _isClosing = true;
            _screenTextCaptureService?.StopForShutdown();
            _panelDragSession.Cancel();
            CloseWindow(PanelWindow, "panel window close");
        }

        Unsubscribe();
    }

    private void Unsubscribe()
    {
        ToolbarWindow.TranslationRequested -= OnTranslationRequested;
        ToolbarWindow.ToolMenuRequested -= OnToolMenuRequested;
        ToolbarWindow.SettingsRequested -= OnSettingsRequested;
        ToolbarWindow.ExitRequested -= OnExitRequested;
        ToolbarWindow.DragStarted -= OnToolbarDragStarted;
        ToolbarWindow.DragCompleted -= OnToolbarDragCompleted;
        ToolbarWindow.Closing -= OnToolbarWindowClosing;
        ToolbarWindow.Closed -= OnToolbarWindowClosed;
        ToolbarWindow.NormalPlacementReady -= OnToolbarNormalPlacementReady;
        ToolbarWindow.CaptureRestoreRequested -= OnCaptureRestoreRequested;
        if (_screenTextCaptureService is not null)
        {
            _screenTextCaptureService.CaptureStarting -= OnCaptureStarting;
            _screenTextCaptureService.CaptureFinished -= OnCaptureFinished;
        }
        PanelWindow.CloseRequested -= OnPanelCloseRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private static void CloseWindow(Window window, string stage)
    {
        try
        {
            window.Close();
        }
        catch (Exception exception)
        {
            ReportExitFailure(stage, exception);
        }
    }

    private static void ReportExitFailure(string stage, Exception exception) =>
        Trace.TraceError(
            $"FloatingTools exit {stage} failed: {exception.GetType().Name}: {exception.Message}");
}
