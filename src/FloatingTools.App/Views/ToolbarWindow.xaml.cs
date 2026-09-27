using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;

namespace FloatingTools.App.Views;

public partial class ToolbarWindow : Window
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCornerDefault = 0;
    private const int DwmCornerDoNotRound = 1;
    private const int WmQueryOpen = 0x0013;
    private const int WmSysCommand = 0x0112;
    private const long SystemCommandMask = 0xFFF0;
    private const long ScMinimize = 0xF020;
    private const long ScRestore = 0xF120;
    private static readonly TimeSpan TransientStatusDuration = TimeSpan.FromSeconds(3);
    private readonly WindowPlacementService _placementService;
    private readonly Effect _toolbarShadow;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly ToolTip _transientStatusToolTip;
    private readonly TextBlock _transientStatusText;
    private DispatcherTimer? _transientStatusTimer;
    private long _transientStatusGeneration;

    private IntPtr _windowHandle;
    private PixelPoint _pointerAtDragStart;
    private PixelRect _windowAtDragStart;
    private bool _pointerIsDown;
    private bool _isDragging;
    private bool _sourceInitialized;
    private bool _isPanelConnected;
    private bool _visibilityTransition;
    private bool _applicationMinimizeInProgress;
    private bool _closed;
    private HwndSource? _source;
    private bool _restoreRequestQueued;
    private bool _captureRestorePending;
    private bool _shutdownStarted;

    internal int? DwmCornerPreferenceHResult { get; private set; }

    internal bool DeferRestoreForCapture { get; set; }
    internal event EventHandler? CaptureRestoreRequested;

    internal bool TakePendingCaptureRestore()
    {
        var pending = _captureRestorePending;
        _captureRestorePending = false;
        return pending;
    }

    internal void StopUiRestoration() => _shutdownStarted = true;

    public event EventHandler? NormalPlacementReady;

    public bool CanUseNormalPlacement => _sourceInitialized && !_closed
        && !_visibilityTransition && WindowState == WindowState.Normal
        && !IsIconic(_windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr window, int attribute, ref int value, int valueSize);

    public void CompletePendingDrag() => CompleteDrag(releaseMouseCapture: true);

    public void MinimizeUi()
    {
        if (_closed || WindowState == WindowState.Minimized) return;
        DismissTransientStatus();
        CompletePendingDrag();
        _visibilityTransition = true;
        _applicationMinimizeInProgress = true;
        try { WindowState = WindowState.Minimized; }
        finally { _applicationMinimizeInProgress = false; }
    }

    public void RestoreUi()
    {
        if (_closed || _shutdownStarted || WindowState == WindowState.Normal) return;
        _visibilityTransition = true;
        WindowState = WindowState.Normal;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        _visibilityTransition = true;
        base.OnStateChanged(e);
        if (WindowState == WindowState.Normal)
        {
            // Let native restore finish before reading or persisting geometry.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_closed || _shutdownStarted || WindowState != WindowState.Normal || IsIconic(_windowHandle)) return;
                _visibilityTransition = false;
                NormalPlacementReady?.Invoke(this, EventArgs.Empty);
            }));
        }
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((DeferRestoreForCapture || _shutdownStarted) && WindowState == WindowState.Minimized
            && (message == WmQueryOpen || (message == WmSysCommand && (wParam.ToInt64() & SystemCommandMask) == ScRestore)))
        {
            // WM_QUERYOPEN/SC_RESTORE: finish capture cleanup before opening.
            // Never close an overlay or change focus inside the native callback.
            handled = true;
            if (_shutdownStarted) return IntPtr.Zero;
            _captureRestorePending = true;
            if (!_restoreRequestQueued)
            {
                _restoreRequestQueued = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _restoreRequestQueued = false;
                    if (!_closed && !_shutdownStarted && TakePendingCaptureRestore())
                        CaptureRestoreRequested?.Invoke(this, EventArgs.Empty);
                }));
            }
            return IntPtr.Zero;
        }
        if (message == WmSysCommand && (wParam.ToInt64() & SystemCommandMask) == ScMinimize
            && WindowState != WindowState.Minimized && !_applicationMinimizeInProgress)
        {
            // Block system minimize before it can flash the UI. Intentional
            // application minimization uses MinimizeUi, not SC_MINIMIZE.
            handled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closed && !_shutdownStarted && !DeferRestoreForCapture && WindowState == WindowState.Normal) Activate();
            }));
        }
        return IntPtr.Zero;
    }

    public event EventHandler? TranslationRequested;

    public event EventHandler? ToolMenuRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? DragStarted;

    public event EventHandler? DragCompleted;

    public ToolbarWindow(
        WindowPlacementService placementService,
        SettingsService settingsService,
        AppSettings settings)
    {
        InitializeComponent();
        _toolbarShadow = ShellRoot.Effect;

        _placementService = placementService
            ?? throw new ArgumentNullException(nameof(placementService));
        _settingsService = settingsService
            ?? throw new ArgumentNullException(nameof(settingsService));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));

        _transientStatusText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold
        };
        _transientStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "FloatingToolsBrushStatusWarning");
        _transientStatusToolTip = new ToolTip
        {
            Content = _transientStatusText,
            PlacementTarget = ToolbarBlock,
            StaysOpen = true
        };
        _transientStatusToolTip.SetResourceReference(
            FrameworkElement.StyleProperty,
            "FloatingToolsSharedToolTipStyle");

        ApplyDockVisuals(_settings.WindowPlacement?.DockSide ?? DockSide.Right);
        SourceInitialized += OnSourceInitialized;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
    }

    public void UpdatePanelConnection(bool isPanelConnected)
    {
        _isPanelConnected = isPanelConnected;

        if (_settings.WindowPlacement is not null)
        {
            ApplyDockVisuals(_settings.WindowPlacement.DockSide);
        }
    }

    internal bool IsTransientStatusVisible => _transientStatusToolTip.IsOpen;

    internal string? TransientStatusText => _transientStatusText.Text;

    internal void ShowTransientStatus(string message)
    {
        if (_closed || _shutdownStarted || string.IsNullOrWhiteSpace(message)) return;

        _transientStatusText.Text = message;
        _transientStatusToolTip.Placement = _settings.WindowPlacement?.DockSide switch
        {
            DockSide.Right => PlacementMode.Left,
            DockSide.Top => PlacementMode.Bottom,
            _ => PlacementMode.Right
        };
        _transientStatusToolTip.IsOpen = true;
        _transientStatusTimer?.Stop();
        var generation = ++_transientStatusGeneration;
        var timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TransientStatusDuration
        };
        timer.Tick += (_, _) => DismissTransientStatus(generation);
        _transientStatusTimer = timer;
        timer.Start();
    }

    private void DismissTransientStatus(long generation)
    {
        if (generation == _transientStatusGeneration)
        {
            DismissTransientStatus();
        }
    }

    private void DismissTransientStatus()
    {
        _transientStatusGeneration++;
        _transientStatusTimer?.Stop();
        _transientStatusTimer = null;
        _transientStatusToolTip.IsOpen = false;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(OnWindowMessage);
        if (_settings.WindowPlacement is { DockSide: DockSide.Top } savedTop
            && !_placementService.GetMonitors().Any(monitor =>
                string.Equals(monitor.MonitorId, savedTop.MonitorId,
                    StringComparison.OrdinalIgnoreCase)))
        {
            // Restore will choose the primary right edge. Use its native size first.
            ApplyDockVisuals(DockSide.Right);
        }
        _settings.WindowPlacement = _placementService.Restore(
            _windowHandle,
            _settings.WindowPlacement);
        _sourceInitialized = true;
        ApplyDockVisuals(_settings.WindowPlacement.DockSide);
        _settingsService.Save(_settings);
    }

    private void MainTileButton_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !CanUseNormalPlacement)
        {
            return;
        }

        _pointerIsDown = true;
        _isDragging = false;
        _pointerAtDragStart = _placementService.GetCursorPosition();
        _windowAtDragStart = _placementService.GetWindowBounds(_windowHandle);
    }

    private void MainTileButton_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pointerIsDown)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CompleteDrag(releaseMouseCapture: false);
            return;
        }

        var pointer = _placementService.GetCursorPosition();
        var horizontalMovement = pointer.X - _pointerAtDragStart.X;
        var verticalMovement = pointer.Y - _pointerAtDragStart.Y;

        if (!_isDragging
            && Math.Abs(horizontalMovement) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(verticalMovement) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (!_isDragging)
        {
            BeginToolbarDrag();
        }

        try
        {
            _placementService.MoveWindow(
                _windowHandle,
                _windowAtDragStart.Left + horizontalMovement,
                _windowAtDragStart.Top + verticalMovement);
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            CompleteDrag(releaseMouseCapture: false);
        }

        e.Handled = true;
    }

    private void BeginToolbarDrag()
    {
        _isDragging = true;
        DismissTransientStatus();
        DragStarted?.Invoke(this, EventArgs.Empty);
    }

    private void MainTileButton_OnPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_pointerIsDown)
        {
            return;
        }

        if (!_isDragging)
        {
            _pointerIsDown = false;
            return;
        }

        CompleteDrag(releaseMouseCapture: true);
        e.Handled = true;
    }

    private void MainTileButton_OnLostMouseCapture(
        object sender,
        MouseEventArgs e)
    {
        if (_isDragging)
        {
            CompleteDrag(releaseMouseCapture: false);
            return;
        }

        _pointerIsDown = false;
    }

    private void CompleteDrag(bool releaseMouseCapture)
    {
        if (!_isDragging)
        {
            _pointerIsDown = false;
            return;
        }

        _pointerIsDown = false;
        _isDragging = false;

        if (releaseMouseCapture && MainTileButton.IsMouseCaptured)
        {
            MainTileButton.ReleaseMouseCapture();
        }

        try
        {
            if (!CanUseNormalPlacement) return;
            SnapToNearestDock();
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            // The coordinator still receives completion and restores any
            // temporarily hidden panel without starting a second drag cycle.
        }
        finally
        {
            DragCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    internal WindowPlacement SnapToNearestDock()
    {
        var nearest = _placementService.GetNearestDockPlacement(_windowHandle);
        _settings.WindowPlacement = nearest;
        ApplyDockVisuals(nearest.DockSide);
        // The destination Width/Height must reach the HWND before Resolve
        // positions it against the work-area edge.
        _settings.WindowPlacement = _placementService.Restore(_windowHandle, nearest);
        _settingsService.Save(_settings);
        return _settings.WindowPlacement;
    }

    private void MainTileButton_OnClick(object sender, RoutedEventArgs e)
    {
        TranslationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ToolsMenuButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToolMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyDockVisuals(DockSide dockSide)
    {
        var topDocked = dockSide == DockSide.Top;
        if (_windowHandle != IntPtr.Zero)
        {
            var preference = topDocked ? DwmCornerDoNotRound : DwmCornerDefault;
            DwmCornerPreferenceHResult = DwmSetWindowAttribute(
                _windowHandle, DwmWindowCornerPreference, ref preference, sizeof(int));
        }
        CubeShadowClip.ClipToBounds = topDocked;
        ShellRoot.Effect = topDocked ? null : _toolbarShadow;
        MainTileButton.Effect = topDocked ? _toolbarShadow : null;
        Width = ShellRoot.Width = ToolbarBlock.Width = topDocked ? 48 : 80;
        Height = ShellRoot.Height = ToolbarBlock.Height = topDocked ? 80 : 48;
        Grid.SetRow(CubeShadowClip, 0);
        Grid.SetRow(ToolsMenuButton, topDocked ? 1 : 0);
        MainTileButton.Width = MainTileButton.Height = 48;
        ToolsMenuButton.Width = topDocked ? 48 : 32;
        ToolsMenuButton.Height = topDocked ? 32 : 48;
        ToolsMenuGlyph.Visibility = topDocked ? Visibility.Collapsed : Visibility.Visible;
        TopDockDots.Visibility = topDocked ? Visibility.Visible : Visibility.Collapsed;
        TopDockDotsHitSurface.Visibility = topDocked ? Visibility.Visible : Visibility.Collapsed;

        if (topDocked)
        {
            Grid.SetColumn(CubeShadowClip, 0);
            Grid.SetColumn(ToolsMenuButton, 0);
            SetButtonCornerRadius(MainTileButton, new CornerRadius(0, 0, 12, 12));
            SetButtonCornerRadius(ToolsMenuButton, new CornerRadius(0));
            return;
        }

        var layout = DockedLayoutCalculator.Calculate(dockSide, panelWidth: 0);
        Grid.SetColumn(CubeShadowClip, layout.ToolsButtonPrecedesCube ? 1 : 0);
        Grid.SetColumn(ToolsMenuButton, layout.ToolsButtonPrecedesCube ? 0 : 1);

        const double radius = 12;
        var mainRadius = dockSide switch
        {
            DockSide.Right when _isPanelConnected => new CornerRadius(radius, 0, 0, 0),
            DockSide.Right => new CornerRadius(radius, 0, 0, radius),
            DockSide.Left when _isPanelConnected => new CornerRadius(0, radius, 0, 0),
            DockSide.Left => new CornerRadius(0, radius, radius, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(dockSide))
        };

        SetButtonCornerRadius(MainTileButton, mainRadius);
        SetButtonCornerRadius(ToolsMenuButton, new CornerRadius(0));
    }

    private static void SetButtonCornerRadius(
        Button button,
        CornerRadius cornerRadius)
    {
        button.ApplyTemplate();

        if (button.Template.FindName("ButtonBorder", button) is Border border)
        {
            border.CornerRadius = cornerRadius;
        }
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!CanUseNormalPlacement)
        {
            return;
        }

        if (_settings.WindowPlacement?.DockSide != DockSide.Top)
        {
            _settings.WindowPlacement = _placementService.DockToNearestSide(_windowHandle);
        }
        _settingsService.Save(_settings);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _closed = true;
        DismissTransientStatus();
        _source?.RemoveHook(OnWindowMessage);
        _source = null;
        SourceInitialized -= OnSourceInitialized;
        Closing -= OnWindowClosing;
        Closed -= OnWindowClosed;
    }
}
