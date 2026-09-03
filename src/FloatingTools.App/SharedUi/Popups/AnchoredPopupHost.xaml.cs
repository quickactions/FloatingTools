using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FloatingTools.App.SharedUi.Popups;

/// <summary>
/// Owns WPF measurement, input/scroll lifetime, and translation of the anchor into
/// the clamp element's local DIP coordinate space. Consumers never adjust scroll
/// offsets or mix local coordinates with screen coordinates.
/// </summary>
public partial class AnchoredPopupHost : UserControl, IPopupAnchorHost
{
    private PopupAnchorRequest? _request;
    private FrameworkElement? _content;
    private ScrollViewer? _subscribedScrollViewer;
    private bool _inputSubscribed;
    private bool _isClosing;
    private bool _isRecalculating;

    public AnchoredPopupHost()
    {
        InitializeComponent();
        Unloaded += PopupContext_OnUnloaded;
        IsVisibleChanged += PopupContext_OnIsVisibleChanged;
    }

    public event EventHandler? Closed;

    public bool IsOpen { get; private set; }

    public PopupAnchorRequest? Request => _request;

    public PopupAnchorPlacement CurrentPlacement { get; private set; }

    public Size MeasuredPopupSize { get; private set; }

    internal bool HasScrollSubscription => _subscribedScrollViewer is not null;

    internal bool HasInputSubscription => _inputSubscribed;

    internal bool IsAttachedToVisualTree => PresentationSource.FromVisual(this) is not null;

    internal bool IsPopupOpen => AnchorPopup.IsOpen;

    internal void Show(PopupAnchorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dispatcher.VerifyAccess();

        if (IsOpen)
        {
            Close();
        }

        ValidateRequest(request);
        _request = request;
        _content = request.CreateContent();
        PopupContent.Content = _content;
        _content.SizeChanged += Content_OnSizeChanged;
        request.Target.SizeChanged += PlacementElement_OnSizeChanged;
        request.ClampBoundsElement.SizeChanged += PlacementElement_OnSizeChanged;
        request.Target.Unloaded += PopupContext_OnUnloaded;
        request.Target.IsVisibleChanged += PopupContext_OnIsVisibleChanged;
        request.ClampBoundsElement.Unloaded += PopupContext_OnUnloaded;
        request.ClampBoundsElement.IsVisibleChanged += PopupContext_OnIsVisibleChanged;

        AnchorPopup.PlacementTarget = request.ClampBoundsElement;
        IsOpen = true;
        RecalculatePlacement();
        SubscribeForCloseSignals(request);
        AnchorPopup.IsOpen = true;
    }

    void IPopupAnchorHost.Show(PopupAnchorRequest request) => Show(request);

    public void Close()
    {
        Dispatcher.VerifyAccess();
        if (!IsOpen || _isClosing)
        {
            return;
        }

        _isClosing = true;
        UnsubscribeAll();
        IsOpen = false;
        if (AnchorPopup.IsOpen)
        {
            AnchorPopup.IsOpen = false;
        }

        CompleteClose();
    }

    internal void RecalculatePlacement()
    {
        Dispatcher.VerifyAccess();
        if (!IsOpen || _request is null || _isRecalculating)
        {
            return;
        }

        _isRecalculating = true;
        try
        {
            _content?.InvalidateMeasure();
            PopupContent.InvalidateMeasure();
            PopupSurface.InvalidateMeasure();
            PopupSurface.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var popupSize = PopupSurface.DesiredSize;
            MeasuredPopupSize = popupSize;

            var anchorBounds = _request.Target
                .TransformToVisual(_request.ClampBoundsElement)
                .TransformBounds(new Rect(new Point(), _request.Target.RenderSize));
            var clampBounds = new Rect(
                new Point(), _request.ClampBoundsElement.RenderSize);

            CurrentPlacement = PopupAnchorPlacementCalculator.Calculate(
                anchorBounds,
                popupSize,
                clampBounds,
                _request.PreferredPlacement,
                _request.Gap,
                _request.EdgeMargin);
            AnchorPopup.HorizontalOffset = CurrentPlacement.Position.X;
            AnchorPopup.VerticalOffset = CurrentPlacement.Position.Y;
        }
        finally
        {
            _isRecalculating = false;
        }
    }

    internal void ProcessInteraction(DependencyObject? interactionSource)
    {
        if (!IsOpen
            || _request?.CloseOnExternalClick != true
            || interactionSource is null
            || IsDescendantOrSelf(interactionSource, PopupSurface))
        {
            return;
        }

        Close();
    }

    internal void ProcessScroll(double horizontalChange, double verticalChange)
    {
        if (IsOpen && (horizontalChange != 0 || verticalChange != 0))
        {
            Close();
        }
    }

    internal void ProcessContentSizeChange() => RecalculatePlacement();

    internal static bool IsDescendantOrSelf(
        DependencyObject candidate,
        DependencyObject ancestor)
    {
        for (DependencyObject? current = candidate;
             current is not null;
             current = GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(element);
    }

    private static void ValidateRequest(PopupAnchorRequest request)
    {
        if (!ReferenceEquals(request.Target.Dispatcher, request.ClampBoundsElement.Dispatcher))
        {
            throw new ArgumentException(
                "Target and clamp bounds element must belong to the same Dispatcher.",
                nameof(request));
        }

        if (!double.IsFinite(request.Gap) || request.Gap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Gap must be finite and non-negative.");
        }

        if (!double.IsFinite(request.EdgeMargin) || request.EdgeMargin < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request), "Edge margin must be finite and non-negative.");
        }
    }

    private void SubscribeForCloseSignals(PopupAnchorRequest request)
    {
        if (request.CloseOnExternalClick)
        {
            InputManager.Current.PreProcessInput += InputManager_OnPreProcessInput;
            _inputSubscribed = true;
        }

        if (request.CloseOnScrollOf is not null)
        {
            _subscribedScrollViewer = request.CloseOnScrollOf;
            _subscribedScrollViewer.ScrollChanged += ScrollViewer_OnScrollChanged;
        }
    }

    private void UnsubscribeAll()
    {
        if (_inputSubscribed)
        {
            InputManager.Current.PreProcessInput -= InputManager_OnPreProcessInput;
            _inputSubscribed = false;
        }

        if (_subscribedScrollViewer is not null)
        {
            _subscribedScrollViewer.ScrollChanged -= ScrollViewer_OnScrollChanged;
            _subscribedScrollViewer = null;
        }

        if (_content is not null)
        {
            _content.SizeChanged -= Content_OnSizeChanged;
        }

        if (_request is not null)
        {
            _request.Target.SizeChanged -= PlacementElement_OnSizeChanged;
            _request.ClampBoundsElement.SizeChanged -= PlacementElement_OnSizeChanged;
            _request.Target.Unloaded -= PopupContext_OnUnloaded;
            _request.Target.IsVisibleChanged -= PopupContext_OnIsVisibleChanged;
            _request.ClampBoundsElement.Unloaded -= PopupContext_OnUnloaded;
            _request.ClampBoundsElement.IsVisibleChanged -= PopupContext_OnIsVisibleChanged;
        }
    }

    private void InputManager_OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseButtonEventArgs
            {
                ButtonState: MouseButtonState.Pressed
            } mouseEvent)
        {
            var source = ResolveInteractionSource(
                mouseEvent.OriginalSource as DependencyObject,
                mouseEvent.Source as DependencyObject,
                Mouse.DirectlyOver as DependencyObject);
            ProcessInteraction(source);
        }
    }

    internal static DependencyObject? ResolveInteractionSource(
        DependencyObject? originalSource,
        DependencyObject? routedSource,
        DependencyObject? directlyOver) =>
        originalSource ?? routedSource ?? directlyOver;

    private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        => ProcessScroll(e.HorizontalChange, e.VerticalChange);

    private void Content_OnSizeChanged(object sender, SizeChangedEventArgs e)
        => ProcessContentSizeChange();

    private void PlacementElement_OnSizeChanged(object sender, SizeChangedEventArgs e)
        => RecalculatePlacement();

    private void PopupSurface_OnSizeChanged(object sender, SizeChangedEventArgs e)
        => ProcessContentSizeChange();

    private void PopupContext_OnUnloaded(object sender, RoutedEventArgs e) => Close();

    private void PopupContext_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!IsPopupContextUsable())
        {
            Close();
        }
    }

    private bool IsPopupContextUsable() =>
        _request is not null
        && IsUsable(this)
        && IsUsable(_request.Target)
        && IsUsable(_request.ClampBoundsElement);

    private static bool IsUsable(FrameworkElement element) =>
        element.IsLoaded
        && element.IsVisible
        && PresentationSource.FromVisual(element) is not null;

    private void AnchorPopup_OnClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            Close();
        }
    }

    private void CompleteClose()
    {
        PopupContent.Content = null;
        AnchorPopup.PlacementTarget = null;
        _content = null;
        _request = null;
        _isClosing = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
