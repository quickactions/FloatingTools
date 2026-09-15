using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FloatingTools.App.Controls;

public static class CaretScrollMarginBehavior
{
    public const double DefaultTopMargin = 8;
    public const double DefaultBottomMargin = 24;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(CaretScrollMarginBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty TopMarginProperty =
        DependencyProperty.RegisterAttached(
            "TopMargin",
            typeof(double),
            typeof(CaretScrollMarginBehavior),
            new PropertyMetadata(DefaultTopMargin));

    public static readonly DependencyProperty BottomMarginProperty =
        DependencyProperty.RegisterAttached(
            "BottomMargin",
            typeof(double),
            typeof(CaretScrollMarginBehavior),
            new PropertyMetadata(DefaultBottomMargin));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetTopMargin(DependencyObject element, double value) =>
        element.SetValue(TopMarginProperty, value);

    public static double GetTopMargin(DependencyObject element) =>
        (double)element.GetValue(TopMarginProperty);

    public static void SetBottomMargin(DependencyObject element, double value) =>
        element.SetValue(BottomMarginProperty, value);

    public static double GetBottomMargin(DependencyObject element) =>
        (double)element.GetValue(BottomMarginProperty);

    public static double? CalculateOffset(
        double rectTop,
        double rectBottom,
        double viewportTop,
        double viewportHeight,
        double scrollableHeight,
        double topMargin,
        double bottomMargin)
    {
        if (!AreFinite(rectTop, rectBottom, viewportTop, viewportHeight,
                scrollableHeight, topMargin, bottomMargin)
            || rectBottom < rectTop
            || viewportHeight <= 0)
        {
            return null;
        }

        topMargin = Math.Max(0, topMargin);
        bottomMargin = Math.Max(0, bottomMargin);
        if (rectBottom - rectTop > viewportHeight - topMargin - bottomMargin)
        {
            return null;
        }

        var comfortableTop = viewportTop + topMargin;
        var comfortableBottom = viewportTop + viewportHeight - bottomMargin;
        if (rectTop >= comfortableTop && rectBottom <= comfortableBottom)
        {
            return null;
        }

        var requestedOffset = rectBottom > comfortableBottom
            ? rectBottom + bottomMargin - viewportHeight
            : rectTop - topMargin;
        return Math.Clamp(requestedOffset, 0, Math.Max(0, scrollableHeight));
    }

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if (e.NewValue is true)
        {
            element.AddHandler(
                FrameworkElement.RequestBringIntoViewEvent,
                new RequestBringIntoViewEventHandler(OnRequestBringIntoView),
                true);
        }
        else
        {
            element.RemoveHandler(
                FrameworkElement.RequestBringIntoViewEvent,
                new RequestBringIntoViewEventHandler(OnRequestBringIntoView));
        }
    }

    private static void OnRequestBringIntoView(
        object sender,
        RequestBringIntoViewEventArgs e)
    {
        if (sender is not FrameworkElement content
            || e.Handled
            || e.TargetObject is not Visual target
            || FindAncestor<TextBox>(e.TargetObject) is not { } editor
            || FindNamedAncestor<ItemsControl>(editor, "NoteBlocksItemsControl") is null
            || FindAncestor<ScrollViewer>(content) is not { } scrollViewer
            || e.TargetRect.IsEmpty
            || !AreFinite(e.TargetRect.Top, e.TargetRect.Bottom))
        {
            return;
        }

        Rect requestedRect;
        try
        {
            requestedRect = target.TransformToAncestor(content)
                .TransformBounds(e.TargetRect);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var lineHeight = editor.FontFamily.LineSpacing * editor.FontSize;

        var topMargin = Math.Max(0, GetTopMargin(content));
        var bottomMargin = Math.Max(0, GetBottomMargin(content));
        if (requestedRect.Height <= 0
            || requestedRect.Height > (lineHeight * 1.5) + 1
            || requestedRect.Height > scrollViewer.ViewportHeight - topMargin - bottomMargin)
        {
            return;
        }

        var offset = CalculateOffset(
            requestedRect.Top,
            requestedRect.Bottom,
            scrollViewer.VerticalOffset,
            scrollViewer.ViewportHeight,
            scrollViewer.ScrollableHeight,
            topMargin,
            bottomMargin);
        if (offset is { } requestedOffset
            && Math.Abs(requestedOffset - scrollViewer.VerticalOffset) > 0.01)
        {
            scrollViewer.ScrollToVerticalOffset(requestedOffset);
        }

        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        for (; current is not null; current = GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private static T? FindNamedAncestor<T>(DependencyObject? current, string name)
        where T : FrameworkElement
    {
        for (; current is not null; current = GetParent(current))
        {
            if (current is T match && match.Name == name)
            {
                return match;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }

        if (current is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static bool AreFinite(params double[] values) =>
        values.All(double.IsFinite);
}
