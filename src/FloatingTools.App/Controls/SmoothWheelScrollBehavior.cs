using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FloatingTools.App.Controls;

public static class SmoothWheelScrollBehavior
{
    public const double PixelsPerWheelDetent = 20;
    public const double MaximumPixelsPerEvent = 40;
    private const double SettleThreshold = 0.2;
    private const double ResponseTimeMilliseconds = 42;
    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> States = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothWheelScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static double NormalizeDelta(int wheelDelta)
    {
        var pixels = wheelDelta / (double)Mouse.MouseWheelDeltaForOneLine
            * PixelsPerWheelDetent;
        return Math.Clamp(
            pixels,
            -MaximumPixelsPerEvent,
            MaximumPixelsPerEvent);
    }

    public static double CalculateTargetOffset(
        double currentTarget,
        double movement,
        double scrollableHeight) =>
        Math.Clamp(currentTarget - movement, 0, Math.Max(0, scrollableHeight));

    public static double InterpolateOffset(
        double currentOffset,
        double targetOffset,
        double elapsedMilliseconds)
    {
        if (Math.Abs(targetOffset - currentOffset) <= SettleThreshold)
        {
            return targetOffset;
        }

        var elapsed = Math.Clamp(elapsedMilliseconds, 1, 50);
        var response = 1 - Math.Exp(-elapsed / ResponseTimeMilliseconds);
        return currentOffset + ((targetOffset - currentOffset) * response);
    }

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer viewer)
        {
            return;
        }

        if (e.NewValue is true)
        {
            viewer.PreviewMouseWheel += OnPreviewMouseWheel;
            viewer.Unloaded += OnViewerUnloaded;
        }
        else
        {
            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            viewer.Unloaded -= OnViewerUnloaded;
            if (States.TryGetValue(viewer, out var state))
            {
                state.Stop();
            }
        }
    }

    private static void OnViewerUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer viewer && States.TryGetValue(viewer, out var state))
        {
            state.Stop();
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer
            || e.Handled
            || ShouldPreserveTextEditorScrolling(
                e.OriginalSource as DependencyObject,
                viewer))
        {
            return;
        }

        var state = States.GetValue(viewer, static value => new ScrollState(value));
        var movement = NormalizeDelta(e.Delta);
        var targetOffset = CalculateTargetOffset(
            state.IsActive ? state.TargetOffset : viewer.VerticalOffset,
            movement,
            viewer.ScrollableHeight);
        if (Math.Abs(targetOffset - viewer.VerticalOffset) < 0.01 && !state.IsActive)
        {
            return;
        }

        state.MoveTo(targetOffset);
        e.Handled = true;
    }

    private static bool ShouldPreserveTextEditorScrolling(
        DependencyObject? source,
        ScrollViewer boundary)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is TextBoxBase textBox
                && textBox is TextBox { AcceptsReturn: true }
                && ScrollViewer.GetVerticalScrollBarVisibility(textBox)
                    != ScrollBarVisibility.Disabled)
            {
                return true;
            }

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current) =>
        current is FrameworkContentElement contentElement
            ? contentElement.Parent
            : VisualTreeHelper.GetParent(current);

    private sealed class ScrollState(ScrollViewer viewer)
    {
        private TimeSpan? _lastRenderingTime;

        public double TargetOffset { get; private set; }

        public bool IsActive { get; private set; }

        public void MoveTo(double targetOffset)
        {
            TargetOffset = targetOffset;
            if (IsActive)
            {
                return;
            }

            IsActive = true;
            _lastRenderingTime = null;
            CompositionTarget.Rendering += OnRendering;
        }

        public void Stop()
        {
            if (!IsActive)
            {
                return;
            }

            CompositionTarget.Rendering -= OnRendering;
            IsActive = false;
            _lastRenderingTime = null;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!viewer.IsLoaded)
            {
                Stop();
                return;
            }

            TargetOffset = Math.Clamp(TargetOffset, 0, viewer.ScrollableHeight);
            var renderingTime = (e as RenderingEventArgs)?.RenderingTime;
            var elapsed = renderingTime is { } current && _lastRenderingTime is { } previous
                ? (current - previous).TotalMilliseconds
                : 16;
            _lastRenderingTime = renderingTime;

            var next = InterpolateOffset(viewer.VerticalOffset, TargetOffset, elapsed);
            viewer.ScrollToVerticalOffset(next);
            if (Math.Abs(TargetOffset - next) <= SettleThreshold)
            {
                viewer.ScrollToVerticalOffset(TargetOffset);
                Stop();
            }
        }
    }
}
