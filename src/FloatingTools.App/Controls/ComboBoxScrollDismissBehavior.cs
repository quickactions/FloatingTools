using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace FloatingTools.App.Controls;

public static class ComboBoxScrollDismissBehavior
{
    private static readonly ConditionalWeakTable<ComboBox, Subscription> Subscriptions = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ComboBoxScrollDismissBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ComboBox comboBox)
        {
            return;
        }

        var subscription = Subscriptions.GetValue(
            comboBox,
            static owner => new Subscription(owner));
        if (e.NewValue is true)
        {
            subscription.Enable();
        }
        else
        {
            subscription.Disable();
        }
    }

    private sealed class Subscription(ComboBox comboBox)
    {
        private readonly List<ScrollViewer> _owners = [];
        private bool _isEnabled;

        public void Enable()
        {
            if (_isEnabled)
            {
                return;
            }

            _isEnabled = true;
            comboBox.Loaded += OnLoaded;
            comboBox.Unloaded += OnUnloaded;
            if (comboBox.IsLoaded)
            {
                AttachToOwningScrollViewers();
            }
        }

        public void Disable()
        {
            if (!_isEnabled)
            {
                return;
            }

            _isEnabled = false;
            comboBox.Loaded -= OnLoaded;
            comboBox.Unloaded -= OnUnloaded;
            DetachFromOwningScrollViewers();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) =>
            AttachToOwningScrollViewers();

        private void OnUnloaded(object sender, RoutedEventArgs e) =>
            DetachFromOwningScrollViewers();

        private void AttachToOwningScrollViewers()
        {
            DetachFromOwningScrollViewers();

            for (DependencyObject? current = comboBox;
                 current is not null;
                 current = GetParent(current))
            {
                if (current is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollChanged += OnOwnerScrollChanged;
                    _owners.Add(scrollViewer);
                }
            }
        }

        private void DetachFromOwningScrollViewers()
        {
            foreach (var owner in _owners)
            {
                owner.ScrollChanged -= OnOwnerScrollChanged;
            }

            _owners.Clear();
        }

        private void OnOwnerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0 || e.VerticalChange != 0)
            {
                comboBox.IsDropDownOpen = false;
            }
        }

        private static DependencyObject? GetParent(DependencyObject current)
        {
            var visualParent = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : null;
            if (visualParent is not null)
            {
                return visualParent;
            }

            return current is FrameworkElement element
                ? element.Parent ?? element.TemplatedParent
                : null;
        }
    }
}
