using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FloatingTools.App.SharedUi.Controls;

public sealed class ToolHeaderControl : Button
{
    private const string SecondaryActionPartName = "PART_SecondaryAction";

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ToolHeaderControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsMenuOpenProperty =
        DependencyProperty.Register(
            nameof(IsMenuOpen),
            typeof(bool),
            typeof(ToolHeaderControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsMenuOpenChanged));

    public static readonly DependencyProperty ExpandedContentRootProperty =
        DependencyProperty.Register(
            nameof(ExpandedContentRoot),
            typeof(FrameworkElement),
            typeof(ToolHeaderControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TitleHorizontalAlignmentProperty =
        DependencyProperty.Register(
            nameof(TitleHorizontalAlignment),
            typeof(HorizontalAlignment),
            typeof(ToolHeaderControl),
            new PropertyMetadata(HorizontalAlignment.Center));

    public static readonly DependencyProperty ChevronBrushProperty =
        DependencyProperty.Register(
            nameof(ChevronBrush),
            typeof(Brush),
            typeof(ToolHeaderControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ChevronTemplateProperty =
        DependencyProperty.Register(
            nameof(ChevronTemplate),
            typeof(DataTemplate),
            typeof(ToolHeaderControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ChevronMarginProperty =
        DependencyProperty.Register(
            nameof(ChevronMargin),
            typeof(Thickness),
            typeof(ToolHeaderControl),
            new PropertyMetadata(new Thickness(8, 0, 0, 0)));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(ToolHeaderControl),
            new PropertyMetadata(new CornerRadius()));

    public static readonly DependencyProperty IsSecondaryActionVisibleProperty =
        DependencyProperty.Register(
            nameof(IsSecondaryActionVisible),
            typeof(bool),
            typeof(ToolHeaderControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty SecondaryActionCommandProperty =
        DependencyProperty.Register(
            nameof(SecondaryActionCommand),
            typeof(ICommand),
            typeof(ToolHeaderControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryActionContentProperty =
        DependencyProperty.Register(
            nameof(SecondaryActionContent),
            typeof(object),
            typeof(ToolHeaderControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryActionToolTipProperty =
        DependencyProperty.Register(
            nameof(SecondaryActionToolTip),
            typeof(object),
            typeof(ToolHeaderControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryActionAutomationNameProperty =
        DependencyProperty.Register(
            nameof(SecondaryActionAutomationName),
            typeof(string),
            typeof(ToolHeaderControl),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsMenuOpen
    {
        get => (bool)GetValue(IsMenuOpenProperty);
        set => SetValue(IsMenuOpenProperty, value);
    }

    /// <summary>The consumer-owned visual root rendered while this header is expanded.</summary>
    public FrameworkElement? ExpandedContentRoot
    {
        get => (FrameworkElement?)GetValue(ExpandedContentRootProperty);
        set => SetValue(ExpandedContentRootProperty, value);
    }

    public HorizontalAlignment TitleHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(TitleHorizontalAlignmentProperty);
        set => SetValue(TitleHorizontalAlignmentProperty, value);
    }

    public Brush? ChevronBrush
    {
        get => (Brush?)GetValue(ChevronBrushProperty);
        set => SetValue(ChevronBrushProperty, value);
    }

    public DataTemplate? ChevronTemplate
    {
        get => (DataTemplate?)GetValue(ChevronTemplateProperty);
        set => SetValue(ChevronTemplateProperty, value);
    }

    public Thickness ChevronMargin
    {
        get => (Thickness)GetValue(ChevronMarginProperty);
        set => SetValue(ChevronMarginProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool IsSecondaryActionVisible
    {
        get => (bool)GetValue(IsSecondaryActionVisibleProperty);
        set => SetValue(IsSecondaryActionVisibleProperty, value);
    }

    public ICommand? SecondaryActionCommand
    {
        get => (ICommand?)GetValue(SecondaryActionCommandProperty);
        set => SetValue(SecondaryActionCommandProperty, value);
    }

    public object? SecondaryActionContent
    {
        get => GetValue(SecondaryActionContentProperty);
        set => SetValue(SecondaryActionContentProperty, value);
    }

    public object? SecondaryActionToolTip
    {
        get => GetValue(SecondaryActionToolTipProperty);
        set => SetValue(SecondaryActionToolTipProperty, value);
    }

    public string SecondaryActionAutomationName
    {
        get => (string)GetValue(SecondaryActionAutomationNameProperty);
        set => SetValue(SecondaryActionAutomationNameProperty, value);
    }

    private Window? _outsideClickWindow;
    private Button? _secondaryActionButton;
    private bool _isOutsideClickSubscribed;

    public ToolHeaderControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public override void OnApplyTemplate()
    {
        if (_secondaryActionButton is not null)
        {
            _secondaryActionButton.Click -= OnSecondaryActionClick;
        }

        base.OnApplyTemplate();
        _secondaryActionButton = GetTemplateChild(SecondaryActionPartName) as Button;
        if (_secondaryActionButton is not null)
        {
            _secondaryActionButton.Click += OnSecondaryActionClick;
        }
    }

    private static void OnIsMenuOpenChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs _)
    {
        ((ToolHeaderControl)dependencyObject).UpdateOutsideClickSubscription();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateOutsideClickSubscription();

    private void OnUnloaded(object sender, RoutedEventArgs e) => UnsubscribeOutsideClick();

    private static void OnSecondaryActionClick(object sender, RoutedEventArgs e)
    {
        // The nested button owns this action. Stop its routed Click event from
        // reaching the header button while allowing its bound command to run.
        e.Handled = true;
    }

    private void UpdateOutsideClickSubscription()
    {
        if (!IsMenuOpen)
        {
            UnsubscribeOutsideClick();
            return;
        }

        var window = Window.GetWindow(this);
        if (window is null || ReferenceEquals(window, _outsideClickWindow))
        {
            return;
        }

        UnsubscribeOutsideClick();
        _outsideClickWindow = window;
        _outsideClickWindow.PreviewMouseLeftButtonDown += OnWindowPreviewMouseLeftButtonDown;
        _isOutsideClickSubscribed = true;
    }

    private void UnsubscribeOutsideClick()
    {
        if (!_isOutsideClickSubscribed || _outsideClickWindow is null)
        {
            return;
        }

        _outsideClickWindow.PreviewMouseLeftButtonDown -= OnWindowPreviewMouseLeftButtonDown;
        _outsideClickWindow = null;
        _isOutsideClickSubscribed = false;
    }

    private void OnWindowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsMenuOpen)
        {
            UnsubscribeOutsideClick();
            return;
        }

        if (IsWithin(e.OriginalSource, this) || IsWithin(e.OriginalSource, ExpandedContentRoot))
        {
            return;
        }

        IsMenuOpen = false;
    }

    private static bool IsWithin(object originalSource, DependencyObject? root)
    {
        if (root is null)
        {
            return false;
        }

        for (var current = originalSource as DependencyObject; current is not null; current = GetParent(current))
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is Visual)
        {
            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(element);
    }
}
