using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Popups;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.Controls;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class LinkListPopupAnchorIntegrationTests
{
    [Fact]
    public void RealLinkItemSingleClick_OpensTheConnectedProductionPopupHost()
        => RunSta(() =>
        {
            var clipboard = new RecordingClipboard();
            var viewModel = new NotesToolViewModel(
                new RecordingStore(), TimeSpan.Zero, clipboardService: clipboard);
            viewModel.InitializeAsync().GetAwaiter().GetResult();
            var block = Assert.IsType<LinkListNoteBlock>(viewModel
                .InsertLinkListBlockBeforeAsync(null).GetAwaiter().GetResult());
            viewModel.CommitLinkTokens(block, "https://first.test https://second.test");
            block.Items[0].DisplayName = "A deliberately wide link caption";

            var control = new LinkListBlockControl
            {
                Owner = viewModel,
                DataContext = block
            };
            var host = new AnchoredPopupHost { IsHitTestVisible = false };
            var scrollViewer = new ScrollViewer { Content = control };
            var layout = new Grid();
            layout.Children.Add(scrollViewer);
            layout.Children.Add(host);
            var surface = new Border
            {
                Width = 300,
                Height = 220,
                Background = Brushes.White,
                Child = layout
            };
            control.ClampBoundsElement = surface;
            control.PopupHost = host;

            var window = new Window
            {
                Content = surface,
                Width = 320,
                Height = 260,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                var link = FindDescendant<TextBlock>(
                    control, element => ReferenceEquals(element.DataContext, block.Items[0]));
                Assert.NotNull(link);

                link!.RaiseEvent(new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent
                });

                window.UpdateLayout();
                Assert.True(control.PopupAnchorService.IsOpen);
                Assert.True(host.IsOpen);
                Assert.True(host.IsPopupOpen);
                Assert.True(host.IsAttachedToVisualTree);

                var anchorBounds = link.TransformToVisual(surface).TransformBounds(
                    new Rect(new Point(), link.RenderSize));
                var expectedPopupLeft = anchorBounds.Left
                    + (anchorBounds.Width / 2)
                    - (host.MeasuredPopupSize.Width / 2);
                Assert.Equal(expectedPopupLeft, host.CurrentPlacement.Position.X, 3);
            }
            finally
            {
                control.PopupAnchorService.Close();
                window.Close();
            }
        });

    [Fact]
    public void ActionRequest_UsesExactAnchorNotesSurfaceAndAncestorScrollViewer()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var anchor = new TextBlock { DataContext = fixture.Block.Items[0] };

            fixture.Control.OpenActionToolbar(fixture.Block.Items[0], anchor);

            var request = Assert.IsType<PopupAnchorRequest>(
                fixture.Control.PopupAnchorService.ActiveRequest);
            Assert.Same(anchor, request.Target);
            Assert.Equal(PopupAnchorPreferredPlacement.Below, request.PreferredPlacement);
            Assert.Same(fixture.ClampSurface, request.ClampBoundsElement);
            Assert.Same(fixture.ScrollViewer, request.CloseOnScrollOf);
            Assert.True(request.CloseOnExternalClick);
            Assert.True(request.SingleInstance);
            fixture.Control.PopupAnchorService.Close();
        });

    [Fact]
    public void DifferentLinks_CreateRequestsForTheirExactAnchorElements()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var firstAnchor = new TextBlock { DataContext = fixture.Block.Items[0] };
            var secondAnchor = new TextBlock { DataContext = fixture.Block.Items[1] };

            fixture.Control.OpenActionToolbar(fixture.Block.Items[0], firstAnchor);
            var firstRequest = fixture.Control.PopupAnchorService.ActiveRequest;
            fixture.Control.OpenActionToolbar(fixture.Block.Items[1], secondAnchor);
            var secondRequest = fixture.Control.PopupAnchorService.ActiveRequest;

            Assert.Same(firstAnchor, firstRequest!.Target);
            Assert.Same(secondAnchor, secondRequest!.Target);
            Assert.NotSame(firstRequest.Target, secondRequest.Target);
            Assert.False(fixture.HostFactory.Hosts[0].IsOpen);
            Assert.True(fixture.HostFactory.Hosts[1].IsOpen);
            fixture.Control.PopupAnchorService.Close();
        });

    [Fact]
    public void Edit_ReplacesActionRequestAtSameAnchorWithMeasuredEditContent()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var anchor = new TextBlock { DataContext = fixture.Block.Items[0] };
            fixture.Control.OpenActionToolbar(fixture.Block.Items[0], anchor);
            var actionHost = fixture.HostFactory.Hosts[0];

            fixture.Control.Edit_OnClick(new Button(), new RoutedEventArgs());

            var editHost = fixture.HostFactory.Hosts[1];
            Assert.False(actionHost.IsOpen);
            Assert.True(editHost.IsOpen);
            Assert.Same(anchor, editHost.Request!.Target);
            Assert.Same(fixture.ClampSurface, editHost.Request.ClampBoundsElement);
            Assert.True(editHost.MeasuredSize.Width > actionHost.MeasuredSize.Width);
            Assert.True(editHost.MeasuredSize.Height > actionHost.MeasuredSize.Height);
            fixture.Control.PopupAnchorService.Close();
        });

    [Fact]
    public void EditTabs_PreserveTemporaryNameAndUrlValues()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            OpenEdit(fixture);

            fixture.Control.ActiveEditInput!.Text = "Temporary name";
            fixture.Control.UrlTab_OnClick(new Button(), new RoutedEventArgs());
            Assert.Equal(fixture.Block.Items[0].Url, fixture.Control.ActiveEditInput.Text);
            fixture.Control.ActiveEditInput.Text = "https://temporary.test/path";

            fixture.Control.NameTab_OnClick(new Button(), new RoutedEventArgs());
            Assert.Equal("Temporary name", fixture.Control.ActiveEditInput.Text);
            fixture.Control.UrlTab_OnClick(new Button(), new RoutedEventArgs());
            Assert.Equal("https://temporary.test/path", fixture.Control.ActiveEditInput.Text);
            fixture.Control.PopupAnchorService.Close();
        });

    [Theory]
    [InlineData("שלום", FlowDirection.RightToLeft, TextAlignment.Right)]
    [InlineData("hello", FlowDirection.LeftToRight, TextAlignment.Left)]
    public void EditName_UsesSharedFirstStrongDirection(
        string name,
        FlowDirection expectedDirection,
        TextAlignment expectedAlignment)
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            OpenEdit(fixture);

            fixture.Control.ActiveEditInput!.Text = name;

            Assert.Equal(expectedDirection, fixture.Control.ActiveEditInput.FlowDirection);
            Assert.Equal(expectedAlignment, fixture.Control.ActiveEditInput.TextAlignment);
            fixture.Control.PopupAnchorService.Close();
        });

    [Fact]
    public void EditUrl_RemainsLeftToRightEvenWhenTheNameIsHebrew()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            OpenEdit(fixture);
            fixture.Control.ActiveEditInput!.Text = "שם קישור";

            fixture.Control.UrlTab_OnClick(new Button(), new RoutedEventArgs());

            Assert.Equal(FlowDirection.LeftToRight, fixture.Control.ActiveEditInput.FlowDirection);
            Assert.Equal(TextAlignment.Left, fixture.Control.ActiveEditInput.TextAlignment);
            fixture.Control.PopupAnchorService.Close();
        });

    [Fact]
    public void EnterCommitPath_SavesTemporaryNameAndUrl()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            OpenEdit(fixture);
            fixture.Control.ActiveEditInput!.Text = "Saved name";
            fixture.Control.UrlTab_OnClick(new Button(), new RoutedEventArgs());
            fixture.Control.ActiveEditInput.Text = "https://saved.test/path";

            fixture.Control.SaveEditAsync().GetAwaiter().GetResult();

            Assert.Equal("Saved name", fixture.Block.Items[0].DisplayName);
            Assert.Equal("https://saved.test/path", fixture.Block.Items[0].Url);
            Assert.False(fixture.Control.PopupAnchorService.IsOpen);
        });

    [Fact]
    public void HostOutsideClose_CancelsTemporaryEditValues()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var originalName = fixture.Block.Items[0].DisplayName;
            var originalUrl = fixture.Block.Items[0].Url;
            OpenEdit(fixture);
            fixture.Control.ActiveEditInput!.Text = "Unsaved name";

            fixture.HostFactory.Hosts[^1].Close();

            Assert.Equal(originalName, fixture.Block.Items[0].DisplayName);
            Assert.Equal(originalUrl, fixture.Block.Items[0].Url);
            Assert.Null(fixture.Control.ActiveEditInput);
        });

    [Fact]
    public void HostScrollClose_CancelsTemporaryEditValues()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var originalName = fixture.Block.Items[0].DisplayName;
            OpenEdit(fixture);
            fixture.Control.ActiveEditInput!.Text = "Unsaved on scroll";

            fixture.HostFactory.Hosts[^1].Close();

            Assert.Equal(originalName, fixture.Block.Items[0].DisplayName);
            Assert.False(fixture.Control.PopupAnchorService.IsOpen);
        });

    [Fact]
    public void OpenCopyAndDelete_KeepExistingDomainActions()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var first = fixture.Block.Items[0];
            var anchor = new TextBlock { DataContext = first };

            fixture.Control.OpenActionToolbar(first, anchor);
            fixture.Control.OpenSelectedItemAsync().GetAwaiter().GetResult();
            Assert.Equal([first.Url], fixture.Launcher.Opened);

            fixture.Control.OpenActionToolbar(first, anchor);
            fixture.Control.Copy_OnClick(new Button(), new RoutedEventArgs());
            Assert.Equal(first.Url, fixture.Clipboard.Text);

            fixture.Control.OpenActionToolbar(first, anchor);
            fixture.Control.DeleteSelectedItemAsync().GetAwaiter().GetResult();
            Assert.Single(fixture.Block.Items);
            Assert.DoesNotContain(first, fixture.Block.Items);
        });

    [Fact]
    public void ActionTemplateButton_RemainsClickableInsideHostedContent()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var first = fixture.Block.Items[0];
            fixture.Control.OpenActionToolbar(
                first, new TextBlock { DataContext = first });
            var content = fixture.HostFactory.Hosts[^1].Content!;
            var toolbar = Assert.IsType<StackPanel>(content.FindName("ActionToolbar"));
            var copyButton = toolbar.Children.OfType<Button>()
                .Single(button => Equals(button.ToolTip, "Copy"));

            copyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(first.Url, fixture.Clipboard.Text);
            Assert.False(fixture.Control.PopupAnchorService.IsOpen);
        });

    [Fact]
    public void DoubleClickAndControlClick_OpenDirectlyWithoutActionPopup()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var first = fixture.Block.Items[0];
            var second = fixture.Block.Items[1];

            fixture.Control.HandleLinkActivationAsync(
                    first, new TextBlock(), 2, ModifierKeys.None)
                .GetAwaiter().GetResult();
            fixture.Control.HandleLinkActivationAsync(
                    second, new TextBlock(), 1, ModifierKeys.Control)
                .GetAwaiter().GetResult();

            Assert.Equal([first.Url, second.Url], fixture.Launcher.Opened);
            Assert.False(fixture.Control.PopupAnchorService.IsOpen);
            Assert.Empty(fixture.HostFactory.Hosts);
        });

    [Fact]
    public void SingleClick_OpensTheActionToolbarWithoutLaunchingTheUrl()
        => RunSta(() =>
        {
            var fixture = CreateFixture();
            var item = fixture.Block.Items[0];

            fixture.Control.HandleLinkActivationAsync(
                    item,
                    new TextBlock { DataContext = item },
                    clickCount: 1,
                    modifiers: ModifierKeys.None)
                .GetAwaiter()
                .GetResult();

            Assert.True(fixture.Control.PopupAnchorService.IsOpen);
            Assert.True(fixture.HostFactory.Hosts[^1].IsOpen);
            Assert.Empty(fixture.Launcher.Opened);
            fixture.Control.PopupAnchorService.Close();
        });

    private static void OpenEdit(Fixture fixture)
    {
        var anchor = new TextBlock { DataContext = fixture.Block.Items[0] };
        fixture.Control.OpenActionToolbar(fixture.Block.Items[0], anchor);
        fixture.Control.Edit_OnClick(new Button(), new RoutedEventArgs());
    }

    private static Fixture CreateFixture()
    {
        var clipboard = new RecordingClipboard();
        var launcher = new RecordingLauncher();
        var viewModel = new NotesToolViewModel(
            new RecordingStore(),
            TimeSpan.Zero,
            clipboardService: clipboard,
            linkLauncher: launcher);
        viewModel.InitializeAsync().GetAwaiter().GetResult();
        var block = Assert.IsType<LinkListNoteBlock>(viewModel
            .InsertLinkListBlockBeforeAsync(null).GetAwaiter().GetResult());
        viewModel.CommitLinkTokens(
            block, "https://first.test https://second.test");

        var hostFactory = new RecordingHostFactory();
        var service = new PopupAnchorService(hostFactory.Create);
        var control = new LinkListBlockControl(service)
        {
            Owner = viewModel,
            DataContext = block
        };
        var scrollViewer = new ScrollViewer { Content = control };
        var clampSurface = new Border
        {
            Width = 300,
            Height = 220,
            Background = System.Windows.Media.Brushes.White,
            Child = scrollViewer
        };
        control.ClampBoundsElement = clampSurface;
        clampSurface.Measure(new Size(300, 220));
        clampSurface.Arrange(new Rect(0, 0, 300, 220));
        clampSurface.UpdateLayout();

        return new Fixture(
            control,
            block,
            clampSurface,
            scrollViewer,
            hostFactory,
            clipboard,
            launcher);
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        Assert.Null(failure);
    }

    private static T? FindDescendant<T>(
        DependencyObject root,
        Func<T, bool> predicate)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match && predicate(match))
            {
                return match;
            }

            if (FindDescendant(child, predicate) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private sealed record Fixture(
        LinkListBlockControl Control,
        LinkListNoteBlock Block,
        Border ClampSurface,
        ScrollViewer ScrollViewer,
        RecordingHostFactory HostFactory,
        RecordingClipboard Clipboard,
        RecordingLauncher Launcher);

    private sealed class RecordingHostFactory
    {
        public List<RecordingHost> Hosts { get; } = [];

        public IPopupAnchorHost Create()
        {
            var host = new RecordingHost();
            Hosts.Add(host);
            return host;
        }
    }

    private sealed class RecordingHost : IPopupAnchorHost
    {
        public event EventHandler? Closed;

        public bool IsOpen { get; private set; }

        public PopupAnchorRequest? Request { get; private set; }

        public FrameworkElement? Content { get; private set; }

        public Size MeasuredSize { get; private set; }

        public void Show(PopupAnchorRequest request)
        {
            Request = request;
            Content = request.CreateContent();
            Content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            MeasuredSize = Content.DesiredSize;
            IsOpen = true;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; private set; }

        public void SetText(string text) => Text = text;
    }

    private sealed class RecordingLauncher : INoteLinkLauncher
    {
        public List<string> Opened { get; } = [];

        public Task<bool> TryOpenAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            Opened.Add(url);
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingStore : INotesStore
    {
        public Task<NotesStorageState> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new NotesStorageState());

        public Task SaveAsync(
            NotesStorageState state,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
