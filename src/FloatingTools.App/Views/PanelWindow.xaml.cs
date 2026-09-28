using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Views;

public partial class PanelWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private static readonly IntPtr HtTransparent = new(-1);

    internal NotesToolView? ExistingNotesView => NotesTool.Content as NotesToolView;
    private readonly TranslationToolViewModel _translationToolViewModel;
    private readonly NotesToolViewModel _notesToolViewModel;
    private readonly QuickChatViewModel _quickChatViewModel;
    private readonly CalendarToolViewModel _calendarToolViewModel;
    private readonly SettingsViewModel _applicationSettingsViewModel;
    private CornerRadius _activeContentCornerRadius;
    private DockSide _currentDockSide = DockSide.Right;
    private HwndSource? _windowSource;

    public event EventHandler? CloseRequested;

    internal double EffectiveZoomPercentage { get; private set; } =
        PanelZoomCalculator.DefaultPercentage;

    public PanelWindow(
        FloatingToolbarViewModel viewModel,
        TranslationToolViewModel translationToolViewModel,
        NotesToolViewModel notesToolViewModel,
        QuickChatViewModel quickChatViewModel,
        CalendarToolViewModel calendarToolViewModel,
        SettingsViewModel applicationSettingsViewModel)
    {
        InitializeComponent();
        DataContext = viewModel
            ?? throw new ArgumentNullException(nameof(viewModel));

        // View models stay eagerly owned (they hold the tools' live state and
        // are shared with the coordinator); only their views are deferred.
        _translationToolViewModel = translationToolViewModel
            ?? throw new ArgumentNullException(nameof(translationToolViewModel));
        _notesToolViewModel = notesToolViewModel
            ?? throw new ArgumentNullException(nameof(notesToolViewModel));
        _quickChatViewModel = quickChatViewModel
            ?? throw new ArgumentNullException(nameof(quickChatViewModel));
        _calendarToolViewModel = calendarToolViewModel
            ?? throw new ArgumentNullException(nameof(calendarToolViewModel));
        _applicationSettingsViewModel = applicationSettingsViewModel
            ?? throw new ArgumentNullException(nameof(applicationSettingsViewModel));
    }

    /// <summary>
    /// Builds a hosted surface the first time it is shown and leaves it in place
    /// afterwards, so a tool is constructed at most once per session and keeps
    /// its state when the user switches away and back.
    /// </summary>
    private void ToolHost_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is ContentControl { IsVisible: true } host)
        {
            EnsureHostContent(host);
        }
    }

    private void EnsureHostContent(ContentControl host)
    {
        if (host.Content is not null)
        {
            return;
        }

        host.Content = host.Name switch
        {
            nameof(TranslationTool) =>
                CreateTranslationView(),
            nameof(NotesTool) =>
                new NotesToolView { DataContext = _notesToolViewModel },
            nameof(QuickChatTool) =>
                CreateQuickChatView(),
            nameof(CalendarTool) =>
                CreateCalendarView(),
            nameof(ApplicationSettings) =>
                new ApplicationSettingsView { DataContext = _applicationSettingsViewModel },
            _ => host.Content
        };
    }

    private TranslationToolView CreateTranslationView()
    {
        var view = new TranslationToolView { DataContext = _translationToolViewModel };
        view.SetTopDocked(_currentDockSide == DockSide.Top);
        return view;
    }

    private QuickChatToolView CreateQuickChatView()
    {
        var view = new QuickChatToolView { DataContext = _quickChatViewModel };
        view.SetTopDocked(_currentDockSide == DockSide.Top);
        return view;
    }

    private CalendarToolView CreateCalendarView()
    {
        var view = new CalendarToolView { DataContext = _calendarToolViewModel };
        if (DataContext is FloatingToolbarViewModel toolbarViewModel)
        {
            view.SetTopDocked(_currentDockSide == DockSide.Top,
                toolbarViewModel.ActiveToolPanelSize);
        }
        return view;
    }

    private T GetOrCreateToolView<T>(ContentControl host)
        where T : class
    {
        EnsureHostContent(host);
        return (T)host.Content;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Bubble only: child editors and popups get the first opportunity to
        // cancel their own UI. Reuse the close button's Notes-save lifecycle.
        if (!e.Handled && e.Key == Key.Escape
            && DataContext is FloatingToolbarViewModel { PanelState: not PanelState.Closed })
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void FindCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = DataContext is FloatingToolbarViewModel viewModel
            && viewModel.PanelState == PanelState.ActiveTool
            && ToolSupportsSearch(viewModel.ActiveTool);
    }

    private void FindCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (DataContext is not FloatingToolbarViewModel viewModel)
        {
            return;
        }

        switch (viewModel.ActiveTool)
        {
            // Ctrl+F can arrive before the host's visibility change has been
            // processed, so resolve through the same create-once path rather
            // than assuming the view already exists.
            case ToolId.Translation:
                GetOrCreateToolView<TranslationToolView>(TranslationTool).FocusSearch();
                break;
            case ToolId.Notes:
                GetOrCreateToolView<NotesToolView>(NotesTool).FocusSearch();
                break;
            case ToolId.Calendar:
                GetOrCreateToolView<CalendarToolView>(CalendarTool).FocusSearch();
                break;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowSource = PresentationSource.FromVisual(this) as HwndSource;
        _windowSource?.AddHook(WindowMessageHook);
    }

    protected override void OnClosed(EventArgs e)
    {
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        base.OnClosed(e);
    }

    internal bool IsInsideVisiblePanel(Point windowPoint)
    {
        if (!VisiblePanelHost.IsVisible
            || VisiblePanelHost.ActualWidth <= 0
            || VisiblePanelHost.ActualHeight <= 0)
        {
            return false;
        }

        var origin = VisiblePanelHost.TranslatePoint(new Point(), this);
        return new Rect(
            origin,
            new Size(
                VisiblePanelHost.ActualWidth,
                VisiblePanelHost.ActualHeight)).Contains(windowPoint);
    }

    private IntPtr WindowMessageHook(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmNcHitTest)
        {
            return IntPtr.Zero;
        }

        var screenPoint = GetScreenPoint(lParam);
        if (IsInsideVisiblePanel(PointFromScreen(screenPoint)))
        {
            return IntPtr.Zero;
        }

        handled = true;
        return HtTransparent;
    }
    private static Point GetScreenPoint(IntPtr packedPoint)
    {
        var value = packedPoint.ToInt64();
        return new Point(
            unchecked((short)(value & 0xFFFF)),
            unchecked((short)((value >> 16) & 0xFFFF)));
    }

    internal static bool ToolSupportsSearch(ToolId tool) =>
        tool is ToolId.Translation or ToolId.Notes or ToolId.Calendar;

    public ToolSize PrepareVisibleLayout(
        PanelState panelState,
        PanelZoomLayout activeToolLayout,
        DockSide dockSide,
        double visibleTopOffsetDip,
        TopOpeningDirection topOpeningDirection = TopOpeningDirection.Right,
        ToolSize? toolMenuSize = null)
    {
        _currentDockSide = dockSide;
        ToolSize visibleSize;
        if (panelState == PanelState.ToolMenu)
        {
            visibleSize = toolMenuSize ?? new ToolSize(
                PanelSizeCalculator.ToolMenuWidth,
                PanelSizeCalculator.GetToolMenuHeight(
                    Enum.GetValues<ToolId>().Length));
        }
        else
        {
            visibleSize = activeToolLayout.WindowSize;
            EffectiveZoomPercentage = activeToolLayout.EffectivePercentage;
            var scale = activeToolLayout.EffectivePercentage / 100d;
            ActiveToolScaleTransform.ScaleX = scale;
            ActiveToolScaleTransform.ScaleY = scale;
        }

        if (TranslationTool.Content is TranslationToolView translationView)
        {
            translationView.SetTopDocked(dockSide == DockSide.Top);
        }

        if (QuickChatTool.Content is QuickChatToolView quickChatView)
        {
            quickChatView.SetTopDocked(dockSide == DockSide.Top);
        }

        if (CalendarTool.Content is CalendarToolView calendarView
            && DataContext is FloatingToolbarViewModel toolbarViewModel)
        {
            calendarView.SetTopDocked(dockSide == DockSide.Top,
                toolbarViewModel.ActiveToolPanelSize);
        }

        VisiblePanelHost.Width = visibleSize.Width;
        VisiblePanelHost.Height = visibleSize.Height;
        VisiblePanelHost.HorizontalAlignment = dockSide == DockSide.Left
            || (dockSide == DockSide.Top && topOpeningDirection == TopOpeningDirection.Right)
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        VisiblePanelHost.Margin = new Thickness(
            0,
            Math.Max(0, visibleTopOffsetDip),
            0,
            0);
        ApplyCornerRadii(dockSide, topOpeningDirection);
        return visibleSize;
    }

    public void SetHostSize(ToolSize hostSize)
    {
        ArgumentNullException.ThrowIfNull(hostSize);
        Width = hostSize.Width;
        Height = hostSize.Height;
    }

    private void PanelSizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void PanelSizeContextMenu_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu
            || DataContext is not FloatingToolbarViewModel viewModel)
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.IsChecked = item.Tag is PanelSizePreset preset
                && preset == viewModel.ActiveToolPanelSize;
        }
    }

    private void PanelSizeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PanelSizePreset preset }
            && DataContext is FloatingToolbarViewModel viewModel)
        {
            viewModel.SelectPanelSizeCommand.Execute(preset);
        }
    }

    private void ApplyCornerRadii(DockSide dockSide, TopOpeningDirection opening)
    {
        var radii = PanelChromeCornerRadiusCalculator.Calculate(dockSide, opening);

        ToolMenuPanel.CornerRadius = radii.Panel;
        ActiveToolPanel.CornerRadius = radii.Panel;
        ToolMenuHeader.CornerRadius = radii.Header;
        ActiveToolHeader.CornerRadius = radii.Header;
        _activeContentCornerRadius = radii.ActiveContent;
        UpdateActiveToolContentClip();
    }

    private void ActiveToolContent_OnSizeChanged(
        object sender,
        SizeChangedEventArgs e) =>
        UpdateActiveToolContentClip();

    private void UpdateActiveToolContentClip()
    {
        var width = ActiveToolContent.ActualWidth;
        var height = ActiveToolContent.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            ActiveToolContent.Clip = null;
            return;
        }

        var maximumRadius = Math.Min(width, height);
        var bottomRight = Math.Min(_activeContentCornerRadius.BottomRight, maximumRadius);
        var bottomLeft = Math.Min(_activeContentCornerRadius.BottomLeft, maximumRadius);
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            context.LineTo(new Point(width, 0), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(width, height - bottomRight), isStroked: true, isSmoothJoin: false);

            if (bottomRight > 0)
            {
                context.ArcTo(
                    new Point(width - bottomRight, height),
                    new Size(bottomRight, bottomRight),
                    rotationAngle: 0,
                    isLargeArc: false,
                    SweepDirection.Clockwise,
                    isStroked: true,
                    isSmoothJoin: false);
            }

            context.LineTo(new Point(bottomLeft, height), isStroked: true, isSmoothJoin: false);

            if (bottomLeft > 0)
            {
                context.ArcTo(
                    new Point(0, height - bottomLeft),
                    new Size(bottomLeft, bottomLeft),
                    rotationAngle: 0,
                    isLargeArc: false,
                    SweepDirection.Clockwise,
                    isStroked: true,
                    isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        ActiveToolContent.Clip = geometry;
    }
}
