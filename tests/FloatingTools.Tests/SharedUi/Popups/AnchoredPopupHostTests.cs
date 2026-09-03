using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FloatingTools.App.SharedUi.Popups;

namespace FloatingTools.Tests.SharedUi.Popups;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class AnchoredPopupHostTests
{
    [Fact]
    public void Show_MeasuresContentAndUsesMeasuredSizeForPlacement()
        => RunSta(() =>
        {
            var fixture = CreateFixture(new Border { Width = 120, Height = 33 });

            fixture.Host.Show(fixture.Request);

            Assert.Equal(new Size(120, 33), fixture.Host.MeasuredPopupSize);
            Assert.Equal(new Point(40, 64), fixture.Host.CurrentPlacement.Position);
            fixture.Host.Close();
        });

    [Fact]
    public void RecalculatePlacement_UsesNewContentSize()
        => RunSta(() =>
        {
            var content = new Border { Width = 120, Height = 33 };
            var fixture = CreateFixture(content);
            fixture.Host.Show(fixture.Request);
            var first = fixture.Host.CurrentPlacement;

            content.Width = 188;
            content.Height = 62;
            fixture.Host.RecalculatePlacement();

            Assert.Equal(new Size(188, 62), fixture.Host.MeasuredPopupSize);
            Assert.NotEqual(first.Position.X, fixture.Host.CurrentPlacement.Position.X);
            Assert.Equal(6, fixture.Host.CurrentPlacement.Position.X);
            fixture.Host.Close();
        });

    [Fact]
    public void ContentSizeChange_RecalculatesPlacementAutomatically()
        => RunSta(() =>
        {
            var content = new Border { Width = 120, Height = 33 };
            var fixture = CreateFixture(content);
            fixture.Host.Show(fixture.Request);

            content.Width = 188;
            content.Height = 62;
            fixture.Host.ProcessContentSizeChange();

            Assert.Equal(new Size(188, 62), fixture.Host.MeasuredPopupSize);
            Assert.Equal(6, fixture.Host.CurrentPlacement.Position.X);
            fixture.Host.Close();
        });

    [Fact]
    public void ProcessScroll_MeaningfulMovementClosesAndUnsubscribes()
        => RunSta(() =>
        {
            var scrollViewer = new ScrollViewer();
            var fixture = CreateFixture(
                new Border { Width = 120, Height = 33 },
                scrollViewer: scrollViewer);
            fixture.Host.Show(fixture.Request);
            Assert.True(fixture.Host.HasScrollSubscription);

            fixture.Host.ProcessScroll(0, 1);

            Assert.False(fixture.Host.IsOpen);
            Assert.False(fixture.Host.HasScrollSubscription);
        });

    [Fact]
    public void ProcessScroll_LayoutOnlyChangeDoesNotClose()
        => RunSta(() =>
        {
            var fixture = CreateFixture(
                new Border { Width = 120, Height = 33 },
                scrollViewer: new ScrollViewer());
            fixture.Host.Show(fixture.Request);

            fixture.Host.ProcessScroll(0, 0);

            Assert.True(fixture.Host.IsOpen);
            fixture.Host.Close();
        });

    [Fact]
    public void Close_RemovesScrollAndInputSubscriptions()
        => RunSta(() =>
        {
            var fixture = CreateFixture(
                new Border { Width = 120, Height = 33 },
                scrollViewer: new ScrollViewer());
            fixture.Host.Show(fixture.Request);
            Assert.True(fixture.Host.HasScrollSubscription);
            Assert.True(fixture.Host.HasInputSubscription);

            fixture.Host.Close();

            Assert.False(fixture.Host.HasScrollSubscription);
            Assert.False(fixture.Host.HasInputSubscription);
        });

    [Fact]
    public void ProcessInteraction_InsideButtonDoesNotClose()
        => RunSta(() =>
        {
            var button = new Button { Content = "Save", Width = 80, Height = 30 };
            var fixture = CreateFixture(button);
            fixture.Host.Show(fixture.Request);

            fixture.Host.ProcessInteraction(button);

            Assert.True(fixture.Host.IsOpen);
            fixture.Host.Close();
        });

    [Fact]
    public void ProcessInteraction_OutsideElementClosesWhenRequested()
        => RunSta(() =>
        {
            var fixture = CreateFixture(new Border { Width = 120, Height = 33 });
            fixture.Host.Show(fixture.Request);

            fixture.Host.ProcessInteraction(new Button());

            Assert.False(fixture.Host.IsOpen);
        });

    [Fact]
    public void ProcessInteraction_TextBoxAndFocusWithinPopupDoNotClose()
        => RunSta(() =>
        {
            var panel = new StackPanel();
            var first = new TextBox { Width = 120, Height = 30 };
            var second = new TextBox { Width = 120, Height = 30 };
            panel.Children.Add(first);
            panel.Children.Add(second);
            var fixture = CreateFixture(panel);
            fixture.Host.Show(fixture.Request);

            fixture.Host.ProcessInteraction(first);
            fixture.Host.ProcessInteraction(second);

            Assert.True(fixture.Host.IsOpen);
            fixture.Host.Close();
        });

    [Fact]
    public void ProcessInteraction_OutsideDoesNotCloseWhenDisabled()
        => RunSta(() =>
        {
            var fixture = CreateFixture(
                new Border { Width = 120, Height = 33 },
                closeOnExternalClick: false);
            fixture.Host.Show(fixture.Request);

            fixture.Host.ProcessInteraction(new Button());

            Assert.True(fixture.Host.IsOpen);
            fixture.Host.Close();
        });

    [Theory]
    [InlineData(0, 5)]
    [InlineData(260, 175)]
    public void Show_InDisplayedVisualTree_ClampsLeftAndRightEdges(
        double targetLeft,
        double expectedPopupLeft)
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(targetLeft);
            fixture.Host.Show(fixture.Request);

            Assert.Equal(expectedPopupLeft, fixture.Host.CurrentPlacement.Position.X, 3);
        });

    [Fact]
    public void ProcessInteraction_OutsideVisibleContextClosesWithoutHandlingTheInput()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(targetLeft: 80);
            fixture.Host.Show(fixture.Request);

            fixture.Host.ProcessInteraction(fixture.Clamp);

            Assert.False(fixture.Host.IsOpen);
            Assert.False(fixture.Host.IsPopupOpen);
        });

    [Fact]
    public void ProcessInteraction_VisiblePopupChildControlRemainsOpen()
        => RunSta(() =>
        {
            var popupButton = new Button { Content = "Save", Width = 80, Height = 30 };
            using var fixture = CreateDisplayedFixture(targetLeft: 80, popupButton);
            fixture.Host.Show(fixture.Request);

            fixture.Host.ProcessInteraction(popupButton);

            Assert.True(fixture.Host.IsOpen);
            Assert.True(fixture.Host.IsPopupOpen);
        });

    [Fact]
    public void ResolveInteractionSource_UsesRoutedSourceWhenPreprocessSourceIsUnavailable()
        => RunSta(() =>
        {
            var routedSource = new Button();

            var source = AnchoredPopupHost.ResolveInteractionSource(
                originalSource: null,
                routedSource: routedSource,
                directlyOver: null);

            Assert.Same(routedSource, source);
        });

    [Fact]
    public void OwnerContextBecomingHidden_ClosesTheDisplayedPopup()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(targetLeft: 80);
            fixture.Host.Show(fixture.Request);
            Assert.True(fixture.Host.IsPopupOpen);

            fixture.Clamp.Visibility = Visibility.Collapsed;
            fixture.Window.UpdateLayout();

            Assert.False(fixture.Host.IsOpen);
            Assert.False(fixture.Host.IsPopupOpen);
        });

    [Fact]
    public void OwningWindowClosing_ClosesTheDisplayedPopup()
        => RunSta(() =>
        {
            using var fixture = CreateDisplayedFixture(targetLeft: 80);
            fixture.Host.Show(fixture.Request);
            Assert.True(fixture.Host.IsPopupOpen);

            fixture.Window.Close();

            Assert.False(fixture.Host.IsOpen);
            Assert.False(fixture.Host.IsPopupOpen);
        });

    private static PopupFixture CreateFixture(
        FrameworkElement content,
        ScrollViewer? scrollViewer = null,
        bool closeOnExternalClick = true)
    {
        var clamp = new Grid { Width = 300, Height = 220 };
        var target = new Border
        {
            Width = 40,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(80, 40, 0, 0)
        };
        clamp.Children.Add(target);
        clamp.Measure(new Size(300, 220));
        clamp.Arrange(new Rect(0, 0, 300, 220));
        clamp.UpdateLayout();

        var request = new PopupAnchorRequest(target, content, clamp)
        {
            CloseOnExternalClick = closeOnExternalClick,
            CloseOnScrollOf = scrollViewer
        };
        return new PopupFixture(new AnchoredPopupHost(), request);
    }

    private static DisplayedPopupFixture CreateDisplayedFixture(
        double targetLeft,
        FrameworkElement? popupContent = null)
    {
        var layout = new Grid();
        var target = new Border
        {
            Width = 40,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(targetLeft, 40, 0, 0)
        };
        var host = new AnchoredPopupHost { IsHitTestVisible = false };
        layout.Children.Add(target);
        layout.Children.Add(host);
        var clamp = new Border
        {
            Width = 300,
            Height = 220,
            Background = Brushes.White,
            Child = layout
        };
        var window = new Window
        {
            Content = clamp,
            Width = 320,
            Height = 260,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        window.Show();
        window.UpdateLayout();

        var request = new PopupAnchorRequest(
            target,
            popupContent ?? new Border { Width = 120, Height = 33 },
            clamp);
        return new DisplayedPopupFixture(window, clamp, host, request);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private sealed record PopupFixture(
        AnchoredPopupHost Host,
        PopupAnchorRequest Request);

    private sealed class DisplayedPopupFixture : IDisposable
    {
        public DisplayedPopupFixture(
            Window window,
            Border clamp,
            AnchoredPopupHost host,
            PopupAnchorRequest request)
        {
            Window = window;
            Clamp = clamp;
            Host = host;
            Request = request;
        }

        public Window Window { get; }

        public Border Clamp { get; }

        public AnchoredPopupHost Host { get; }

        public PopupAnchorRequest Request { get; }

        public void Dispose()
        {
            Host.Close();
            if (Window.IsLoaded)
            {
                Window.Close();
            }
        }
    }
}
