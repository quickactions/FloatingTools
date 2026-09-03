using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;

namespace FloatingTools.App.Views;

public partial class ToolbarWindow : Window
{
    private readonly WindowPlacementService _placementService;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    private IntPtr _windowHandle;
    private PixelPoint _pointerAtDragStart;
    private PixelRect _windowAtDragStart;
    private bool _pointerIsDown;
    private bool _isDragging;
    private bool _sourceInitialized;
    private bool _isPanelConnected;

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

        _placementService = placementService
            ?? throw new ArgumentNullException(nameof(placementService));
        _settingsService = settingsService
            ?? throw new ArgumentNullException(nameof(settingsService));
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));

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

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
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
        if (e.LeftButton != MouseButtonState.Pressed || !_sourceInitialized)
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
            _isDragging = true;
            DragStarted?.Invoke(this, EventArgs.Empty);
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
            _settings.WindowPlacement = _placementService.DockToNearestSide(_windowHandle);
            ApplyDockVisuals(_settings.WindowPlacement.DockSide);
            _settingsService.Save(_settings);
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
        var layout = DockedLayoutCalculator.Calculate(dockSide, panelWidth: 0);
        Grid.SetColumn(MainTileButton, layout.ToolsButtonPrecedesCube ? 1 : 0);
        Grid.SetColumn(ToolsMenuButton, layout.ToolsButtonPrecedesCube ? 0 : 1);

        const double radius = 12;
        var mainRadius = dockSide switch
        {
            DockSide.Right when _isPanelConnected => new CornerRadius(radius, 0, 0, 0),
            DockSide.Right => new CornerRadius(radius, 0, 0, radius),
            DockSide.Left when _isPanelConnected => new CornerRadius(0, radius, 0, 0),
            _ => new CornerRadius(0, radius, radius, 0)
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
        if (!_sourceInitialized)
        {
            return;
        }

        _settings.WindowPlacement = _placementService.DockToNearestSide(_windowHandle);
        _settingsService.Save(_settings);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        Closing -= OnWindowClosing;
        Closed -= OnWindowClosed;
    }
}
