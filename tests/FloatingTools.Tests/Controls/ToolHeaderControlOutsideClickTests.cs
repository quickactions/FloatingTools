using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FloatingTools.App.SharedUi.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class ToolHeaderControlOutsideClickTests
{
    [Fact]
    public void HeaderClick_IsInsideAndItsExistingToggleRunsOnce()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            var toggles = 0;
            fixture.Header.Click += (_, _) =>
            {
                toggles++;
                fixture.Header.IsMenuOpen = !fixture.Header.IsMenuOpen;
            };
            fixture.Header.IsMenuOpen = true;

            var args = RaiseLeftButtonDown(fixture.Header);
            InvokeWindowPreviewHandler(fixture.Header, fixture.Window, args);
            fixture.Header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal(1, toggles);
            Assert.False(fixture.Header.IsMenuOpen);
            Assert.False(args.Handled);
        });
    }

    [Fact]
    public void ExpandedContentDescendant_IsInsideAndRemainsOpen()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            fixture.Header.IsMenuOpen = true;

            var args = RaiseLeftButtonDown(fixture.InsideTextBox);
            InvokeWindowPreviewHandler(fixture.Header, fixture.Window, args);

            Assert.True(fixture.Header.IsMenuOpen);
            Assert.False(args.Handled);
        });
    }

    [Fact]
    public void OutsideClick_ClosesAndRemainsUnhandled()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            fixture.Header.IsMenuOpen = true;

            var args = RaiseLeftButtonDown(fixture.Window);

            Assert.False(fixture.Header.IsMenuOpen);
            Assert.False(args.Handled);
        });
    }

    [Fact]
    public void OutsideChild_ClosesAndRemainsUnhandled()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            fixture.Header.IsMenuOpen = true;

            var args = RaiseLeftButtonDown(fixture.OutsideButton);
            InvokeWindowPreviewHandler(fixture.Header, fixture.Window, args);

            Assert.False(fixture.Header.IsMenuOpen);
            Assert.False(args.Handled);
        });
    }

    [Fact]
    public void OutsideClick_UpdatesTheBoundOpenState()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            var source = new OpenStateSource { IsOpen = true };
            fixture.Header.SetBinding(
                ToolHeaderControl.IsMenuOpenProperty,
                new Binding(nameof(OpenStateSource.IsOpen)) { Source = source });

            RaiseLeftButtonDown(fixture.Window);

            Assert.False(fixture.Header.IsMenuOpen);
            Assert.False(source.IsOpen);
        });
    }

    [Fact]
    public void Collapse_UnsubscribesImmediately()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            fixture.Header.IsMenuOpen = true;
            Assert.True(IsOutsideClickListenerAttached(fixture.Header));

            fixture.Header.IsMenuOpen = false;

            Assert.False(IsOutsideClickListenerAttached(fixture.Header));
        });
    }

    [Fact]
    public void Unloaded_Unsubscribes()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            fixture.Header.IsMenuOpen = true;
            Assert.True(IsOutsideClickListenerAttached(fixture.Header));

            fixture.Header.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.False(IsOutsideClickListenerAttached(fixture.Header));
        });
    }

    [Fact]
    public void RepeatedExpandCollapse_DoesNotAccumulateListeners()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();

            for (var index = 0; index < 3; index++)
            {
                fixture.Header.IsMenuOpen = true;
                Assert.True(IsOutsideClickListenerAttached(fixture.Header));
                fixture.Header.IsMenuOpen = false;
                Assert.False(IsOutsideClickListenerAttached(fixture.Header));
            }
        });
    }

    [Fact]
    public void NullExpandedContentRoot_IsSafeAndTreatsOtherClicksAsOutside()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture { Header = { ExpandedContentRoot = null } };
            fixture.Header.IsMenuOpen = true;

            var args = RaiseLeftButtonDown(fixture.OutsideButton);
            InvokeWindowPreviewHandler(fixture.Header, fixture.Window, args);

            Assert.False(fixture.Header.IsMenuOpen);
            Assert.False(args.Handled);
        });
    }

    private static MouseButtonEventArgs RaiseLeftButtonDown(UIElement target)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
        };
        target.RaiseEvent(args);
        return args;
    }

    private static void InvokeWindowPreviewHandler(
        ToolHeaderControl header,
        Window window,
        MouseButtonEventArgs args) =>
        typeof(ToolHeaderControl)
            .GetMethod(
                "OnWindowPreviewMouseLeftButtonDown",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(header, [window, args]);

    private static bool IsOutsideClickListenerAttached(ToolHeaderControl header) =>
        (bool)typeof(ToolHeaderControl)
            .GetField("_isOutsideClickSubscribed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(header)!;

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null) throw new Xunit.Sdk.XunitException(exception.ToString());
    }

    private sealed class HeaderFixture : IDisposable
    {
        public HeaderFixture()
        {
            Header = new ToolHeaderControl { ExpandedContentRoot = ExpandedContentRoot };
            ExpandedContentRoot.Child = InsideTextBox;
            var root = new Grid();
            root.Children.Add(Header);
            root.Children.Add(ExpandedContentRoot);
            root.Children.Add(OutsideButton);
            Window = new Window
            {
                Content = root,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Width = 1,
                Height = 1,
                Left = -10_000,
                Top = -10_000
            };
            Window.Show();
        }

        public Window Window { get; }
        public ToolHeaderControl Header { get; }
        public Border ExpandedContentRoot { get; } = new();
        public TextBox InsideTextBox { get; } = new();
        public Button OutsideButton { get; } = new();

        public void Dispose() => Window.Close();
    }

    private sealed class OpenStateSource : DependencyObject
    {
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                nameof(IsOpen),
                typeof(bool),
                typeof(OpenStateSource),
                new PropertyMetadata(false));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }
    }
}
