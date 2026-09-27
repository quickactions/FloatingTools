using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class ToolStripRuntimeTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr window, int attribute, out int value, int valueSize);

    [Theory]
    [InlineData(DockSide.Left, TopOpeningDirection.Right, 80, 48, 80, 240)]
    [InlineData(DockSide.Right, TopOpeningDirection.Right, 80, 48, 80, 240)]
    [InlineData(DockSide.Top, TopOpeningDirection.Right, 48, 80, 240, 80)]
    [InlineData(DockSide.Top, TopOpeningDirection.Left, 48, 80, 240, 80)]
    public void ChevronOpensMutuallyExclusiveToolMenu(
        DockSide dock, TopOpeningDirection opening,
        double closedWidth, double closedHeight, double expandedWidth, double expandedHeight)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock, opening);
            var toolbar = fixture.Toolbar;
            var viewModel = new FloatingToolbarViewModel();
            viewModel.SelectToolCommand.Execute(ToolId.Notes);
            toolbar.DataContext = viewModel;
            var chevron = (Button)toolbar.FindName("ToolsMenuButton");
            var underlay = (Border)toolbar.FindName("ChevronHitSurface");
            Assert.False(toolbar.IsToolStripExpanded);
            Assert.Equal((closedWidth, closedHeight), (toolbar.Width, toolbar.Height));
            Assert.Equal((chevron.Width, chevron.Height), (underlay.Width, underlay.Height));
            Assert.Equal(1, Assert.IsType<SolidColorBrush>(underlay.Background).Color.A);
            var glyph = (TextBlock)toolbar.FindName("ToolsMenuGlyph");
            var dots = (StackPanel)toolbar.FindName("TopDockDots");
            Assert.Equal(dock == DockSide.Top ? Visibility.Collapsed : Visibility.Visible,
                glyph.Visibility);
            Assert.Equal(dock == DockSide.Top ? Visibility.Visible : Visibility.Collapsed,
                dots.Visibility);
            Click(chevron);
            Assert.True(toolbar.IsToolStripExpanded);
            Assert.Equal((expandedWidth, expandedHeight), (toolbar.Width, toolbar.Height));
            Assert.Equal(PanelState.ToolMenu, viewModel.PanelState);
            Assert.Equal(ToolId.Notes, viewModel.ActiveTool);
            var selected = (Button)toolbar.FindName("NotesTileButton");
            Assert.Equal(
                Assert.IsType<SolidColorBrush>(toolbar.FindResource("FloatingToolsBrushToolPanelSelected")).Color,
                Assert.IsType<SolidColorBrush>(selected.Background).Color);
            var layout = ToolStripLayoutCalculator.Calculate(dock, opening, true, 4);
            var main = (Grid)toolbar.FindName("CubeShadowClip");
            Assert.Equal((layout.MainCell.X, layout.MainCell.Y),
                (Canvas.GetLeft(main), Canvas.GetTop(main)));
            Assert.Equal((layout.ChevronCell.X, layout.ChevronCell.Y),
                (Canvas.GetLeft(chevron), Canvas.GetTop(chevron)));
            foreach (var name in new[] { "TranslationTileButton", "NotesTileButton",
                         "QuickChatTileButton", "CalendarTileButton" })
                Assert.Equal(Visibility.Visible, ((Button)toolbar.FindName(name)).Visibility);
            foreach (var toolCell in layout.ToolCells)
            {
                var button = toolCell.Tool switch
                {
                    ToolId.Translation => (Button)toolbar.FindName("TranslationTileButton"),
                    ToolId.Notes => (Button)toolbar.FindName("NotesTileButton"),
                    ToolId.QuickChat => (Button)toolbar.FindName("QuickChatTileButton"),
                    _ => (Button)toolbar.FindName("CalendarTileButton")
                };
                Assert.Equal((toolCell.Cell.X, toolCell.Cell.Y),
                    (Canvas.GetLeft(button), Canvas.GetTop(button)));
                button.ApplyTemplate();
                Assert.Equal(toolCell.Corners,
                    ((Border)button.Template.FindName("ButtonBorder", button)).CornerRadius);
                var idle = Assert.IsType<SolidColorBrush>(button.Background).Color;
                Assert.Equal((byte)255, idle.A);
            }
            Click(chevron);
            Assert.False(toolbar.IsToolStripExpanded);
            Assert.Equal((closedWidth, closedHeight), (toolbar.Width, toolbar.Height));
            Assert.Equal(PanelState.Closed, viewModel.PanelState);
        });

    [Theory]
    [InlineData(ToolId.Translation, "TranslationTileButton")]
    [InlineData(ToolId.Notes, "NotesTileButton")]
    [InlineData(ToolId.QuickChat, "QuickChatTileButton")]
    [InlineData(ToolId.Calendar, "CalendarTileButton")]
    public void ToolSelectionCollapsesStrip(ToolId tool, string buttonName)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(DockSide.Left);
            var toolbar = fixture.Toolbar;
            var viewModel = new FloatingToolbarViewModel();
            toolbar.DataContext = viewModel;
            Click((Button)toolbar.FindName("ToolsMenuButton"));
            Click((Button)toolbar.FindName(buttonName));
            Assert.False(toolbar.IsToolStripExpanded);
            Assert.Equal(tool, viewModel.ActiveTool);
            Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
        });

    [Theory]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    [InlineData(DockSide.Top)]
    public void ToolsMenuRestoresOriginalThreeDotsVisual(DockSide dock)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock);
            var toolbar = fixture.Toolbar;
            var glyph = (TextBlock)toolbar.FindName("ToolsMenuGlyph");
            var dots = (StackPanel)toolbar.FindName("TopDockDots");
            Assert.Equal("⋮", glyph.Text);
            Assert.Equal("Segoe UI Symbol", glyph.FontFamily.Source);
            Assert.Equal(22, glyph.FontSize);
            Assert.Equal(3, dots.Children.Count);
            foreach (var dot in dots.Children.Cast<Ellipse>())
                Assert.Equal((2.5, 2.5), (dot.Width, dot.Height));
            Assert.Equal(new Thickness(0, 0, 4.25, 0),
                ((Ellipse)dots.Children[0]).Margin);
            Assert.Equal(new Thickness(0, 0, 4.25, 0),
                ((Ellipse)dots.Children[1]).Margin);
            Assert.Equal(new Thickness(0), ((Ellipse)dots.Children[2]).Margin);
            Assert.Equal(dock == DockSide.Top ? Visibility.Collapsed : Visibility.Visible,
                glyph.Visibility);
            Assert.Equal(dock == DockSide.Top ? Visibility.Visible : Visibility.Collapsed,
                dots.Visibility);
            var button = (Button)toolbar.FindName("ToolsMenuButton");
            Assert.Equal(dock == DockSide.Top ? (48d, 32d) : (32d, 48d),
                (button.Width, button.Height));
        });

    [Fact]
    public void MainClickFromToolMenuRestoresLastActiveTool()
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(DockSide.Left);
            var toolbar = fixture.Toolbar;
            var viewModel = (FloatingToolbarViewModel)toolbar.DataContext;
            var translations = 0;
            toolbar.TranslationRequested += (_, _) => translations++;
            Click((Button)toolbar.FindName("ToolsMenuButton"));
            Assert.True(toolbar.IsToolStripExpanded);
            Click((Button)toolbar.FindName("MainTileButton"));
            Assert.False(toolbar.IsToolStripExpanded);
            Assert.Equal(PanelState.ActiveTool, viewModel.PanelState);
            Assert.Equal(1, translations);
            Click((Button)toolbar.FindName("MainTileButton"));
            Assert.Equal(PanelState.Closed, viewModel.PanelState);
            Assert.Equal(2, translations);
        });

    [Theory]
    [InlineData(DockSide.Left, TopOpeningDirection.Right)]
    [InlineData(DockSide.Right, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Left)]
    public void MainAndChevronStayAtSameScreenPositionWhenExpanded(
        DockSide dock, TopOpeningDirection opening)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock, opening);
            var toolbar = fixture.Toolbar;
            toolbar.Show();
            var handle = new WindowInteropHelper(toolbar).Handle;
            var monitor = fixture.Placement.GetMonitors().First(candidate => candidate.IsPrimary);
            fixture.Placement.MoveWindow(handle,
                monitor.WorkArea.Left + monitor.WorkArea.Width / 2,
                monitor.WorkArea.Top + monitor.WorkArea.Height / 2);
            var main = (Button)toolbar.FindName("MainTileButton");
            var chevron = (Button)toolbar.FindName("ToolsMenuButton");
            var beforeMain = main.PointToScreen(new Point());
            var beforeChevron = chevron.PointToScreen(new Point());
            Click(chevron);
            toolbar.UpdateLayout();
            Assert.Equal(beforeMain, main.PointToScreen(new Point()));
            Assert.Equal(beforeChevron, chevron.PointToScreen(new Point()));
        });

    [Theory]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    [InlineData(DockSide.Top)]
    public void SecondaryTileHasFullNativeHitTargetAndSolidDarkerIdle(DockSide dock)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock);
            var toolbar = fixture.Toolbar;
            var viewModel = new FloatingToolbarViewModel();
            viewModel.SelectToolCommand.Execute(ToolId.Notes);
            toolbar.DataContext = viewModel;
            toolbar.Show();
            Click((Button)toolbar.FindName("ToolsMenuButton"));
            toolbar.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            var selected = (Button)toolbar.FindName("NotesTileButton");
            var idle = (Button)toolbar.FindName("TranslationTileButton");
            var selectedColor = Assert.IsType<SolidColorBrush>(selected.Background).Color;
            var idleColor = Assert.IsType<SolidColorBrush>(idle.Background).Color;
            Assert.NotEqual(idleColor, selectedColor);
            Assert.Equal((byte)255, idleColor.A);
            Assert.Equal((byte)255, selectedColor.A);
            var mainColor = Assert.IsType<SolidColorBrush>(
                ((Button)toolbar.FindName("MainTileButton")).Background).Color;
            Assert.True(idleColor.R < mainColor.R);
            Assert.True(selectedColor.R < mainColor.R);
            var handle = new WindowInteropHelper(toolbar).Handle;
            var bounds = fixture.Placement.GetWindowBounds(handle);
            var dpi = VisualTreeHelper.GetDpi(toolbar);
            var origin = idle.TranslatePoint(new Point(), toolbar);
            foreach (var (x, y) in new[] { (0.05, 0.05), (0.5, 0.5), (0.95, 0.95),
                         (0.05, 0.95), (0.95, 0.05) })
            {
                var point = new NativePoint
                {
                    X = bounds.Left + (int)Math.Round((origin.X + x * 48) * dpi.DpiScaleX),
                    Y = bounds.Top + (int)Math.Round((origin.Y + y * 48) * dpi.DpiScaleY)
                };
                var received = WindowFromPoint(point);
                for (var frame = 0; received != handle && frame < 5; frame++)
                {
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                    System.Threading.Thread.Sleep(16);
                    received = WindowFromPoint(point);
                }
                Assert.Equal(handle, received);
            }
        });

    [Theory]
    [InlineData(DockSide.Left, TopOpeningDirection.Right)]
    [InlineData(DockSide.Right, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Left)]
    public void ChevronFullRectangleIsNativeHitTarget(DockSide dock, TopOpeningDirection opening)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock, opening);
            var toolbar = fixture.Toolbar;
            toolbar.Show();
            toolbar.UpdateLayout();
            var button = (Button)toolbar.FindName("ToolsMenuButton");
            var handle = new WindowInteropHelper(toolbar).Handle;
            var dpi = VisualTreeHelper.GetDpi(toolbar);
            foreach (var expanded in new[] { false, true })
            {
                if (expanded)
                {
                    Click(button);
                    toolbar.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                    Assert.True(toolbar.IsToolStripExpanded);
                }
                var bounds = fixture.Placement.GetWindowBounds(handle);
                var origin = button.TranslatePoint(new Point(), toolbar);
                var underlay = (Border)toolbar.FindName("ChevronHitSurface");
                var underlayOrigin = underlay.TranslatePoint(new Point(), toolbar);
                foreach (var (x, y) in new[] { (0.1, 0.1), (0.5, 0.5), (0.9, 0.9), (0.1, 0.9), (0.9, 0.1) })
                {
                    var point = new NativePoint
                    {
                        X = bounds.Left + (int)Math.Round((origin.X + x * button.ActualWidth) * dpi.DpiScaleX),
                        Y = bounds.Top + (int)Math.Round((origin.Y + y * button.ActualHeight) * dpi.DpiScaleY)
                    };
                    var received = WindowFromPoint(point);
                    for (var frame = 0; received != handle && frame < 5; frame++)
                    {
                        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                        System.Threading.Thread.Sleep(16);
                        received = WindowFromPoint(point);
                    }
                    Assert.True(handle == received,
                        $"Native hit missed: expanded={expanded}, dock={dock}, point=({x},{y}), origin={origin}, underlay={underlayOrigin}/{underlay.ActualWidth}x{underlay.ActualHeight}, bounds={bounds}, size={button.ActualWidth}x{button.ActualHeight}");
                }
            }
        });

    [Theory]
    [InlineData(DockSide.Left, TopOpeningDirection.Right)]
    [InlineData(DockSide.Right, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Right)]
    [InlineData(DockSide.Top, TopOpeningDirection.Left)]
    public void ExpansionClampsToWorkArea(DockSide dock, TopOpeningDirection opening)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock, opening);
            var toolbar = fixture.Toolbar;
            toolbar.Show();
            var monitor = fixture.Placement.GetMonitors().First(candidate => candidate.IsPrimary);
            var handle = new WindowInteropHelper(toolbar).Handle;
            var before = fixture.Placement.GetWindowBounds(handle);
            var x = dock == DockSide.Top
                ? opening == TopOpeningDirection.Right
                    ? monitor.WorkArea.Right - before.Width : monitor.WorkArea.Left
                : dock == DockSide.Right
                    ? monitor.WorkArea.Right - before.Width : monitor.WorkArea.Left;
            var y = dock == DockSide.Top
                ? monitor.WorkArea.Top : monitor.WorkArea.Bottom - before.Height;
            fixture.Placement.MoveWindow(handle, x, y);
            Click((Button)toolbar.FindName("ToolsMenuButton"));
            var after = fixture.Placement.GetWindowBounds(handle);
            Assert.True(after.Left >= monitor.WorkArea.Left && after.Right <= monitor.WorkArea.Right,
                $"X: dock={dock}, before={before}, after={after}, work={monitor.WorkArea}");
            Assert.True(after.Top >= monitor.WorkArea.Top && after.Bottom <= monitor.WorkArea.Bottom,
                $"Y: dock={dock}, before={before}, after={after}, work={monitor.WorkArea}");
        });

    [Fact]
    public void DragStartCollapsesExpandedStrip()
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(DockSide.Left);
            var toolbar = fixture.Toolbar;
            toolbar.Show();
            Click((Button)toolbar.FindName("ToolsMenuButton"));
            Assert.True(toolbar.IsToolStripExpanded);
            typeof(ToolbarWindow).GetMethod("BeginToolbarDrag",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(toolbar, null);
            Assert.False(toolbar.IsToolStripExpanded);
            Assert.Equal(48, toolbar.Height);
            toolbar.CompletePendingDrag();
        });

    [Theory]
    [InlineData(DockSide.Left, 0)]
    [InlineData(DockSide.Right, 0)]
    [InlineData(DockSide.Top, 1)]
    public void DockRetainsNativeCornerPreference(DockSide dock, int expected)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock);
            fixture.Toolbar.Show();
            var handle = new WindowInteropHelper(fixture.Toolbar).Handle;
            Assert.Equal(0, fixture.Toolbar.DwmCornerPreferenceHResult);
            Assert.Equal(0, DwmGetWindowAttribute(handle, 33, out var preference, sizeof(int)));
            Assert.Equal(expected, preference);
            Click((Button)fixture.Toolbar.FindName("ToolsMenuButton"));
            Assert.Equal(0, DwmGetWindowAttribute(handle, 33, out preference, sizeof(int)));
            Assert.Equal(expected, preference);
        });

    [Fact]
    public void RestartAlwaysBeginsCollapsed()
        => WpfTestApplication.Run(() =>
        {
            using (var first = new Fixture(DockSide.Top))
            {
                Click((Button)first.Toolbar.FindName("ToolsMenuButton"));
                Assert.True(first.Toolbar.IsToolStripExpanded);
            }
            using var second = new Fixture(DockSide.Top);
            Assert.False(second.Toolbar.IsToolStripExpanded);
            Assert.Equal(48, second.Toolbar.Width);
            Assert.Equal(80, second.Toolbar.Height);
        });

    [Theory]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    [InlineData(DockSide.Top)]
    public void ConnectedPanelSquaresOnlyTheJoiningChevronEdge(DockSide dock)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(dock);
            var toolbar = fixture.Toolbar;
            var main = (Button)toolbar.FindName("MainTileButton");
            var chevron = (Button)toolbar.FindName("ToolsMenuButton");
            var originalMain = ((Border)main.Template.FindName("ButtonBorder", main)).CornerRadius;
            toolbar.UpdatePanelConnection(true);
            Assert.Equal(originalMain,
                ((Border)main.Template.FindName("ButtonBorder", main)).CornerRadius);
            Assert.Equal(new CornerRadius(0),
                ((Border)chevron.Template.FindName("ButtonBorder", chevron)).CornerRadius);
            toolbar.UpdatePanelConnection(false);
            Assert.Equal(dock == DockSide.Top ? new CornerRadius(0) :
                ToolStripLayoutCalculator.Calculate(dock, TopOpeningDirection.Right, false, 4).ChevronCorners,
                ((Border)chevron.Template.FindName("ButtonBorder", chevron)).CornerRadius);
        });

    [Theory]
    [InlineData(DockSide.Top, DockSide.Right)]
    [InlineData(DockSide.Top, DockSide.Left)]
    [InlineData(DockSide.Left, DockSide.Right)]
    [InlineData(DockSide.Right, DockSide.Left)]
    public void SnapUsesFinalClosedGeometry(DockSide initial, DockSide destination)
        => WpfTestApplication.Run(() =>
        {
            using var fixture = new Fixture(initial);
            var toolbar = fixture.Toolbar;
            toolbar.Show();
            var service = fixture.Placement;
            var monitor = service.GetMonitors().First(candidate => candidate.IsPrimary);
            var handle = new WindowInteropHelper(toolbar).Handle;
            var bounds = service.GetWindowBounds(handle);
            var x = destination == DockSide.Right ? monitor.WorkArea.Right - bounds.Width
                : monitor.WorkArea.Left;
            var y = destination == DockSide.Top ? monitor.WorkArea.Top
                : monitor.WorkArea.Top + 100;
            service.MoveWindow(handle, x, y);
            var result = toolbar.SnapToNearestDock();
            var final = service.GetWindowBounds(handle);
            Assert.Equal(destination, result.DockSide);
            Assert.Equal(destination == DockSide.Top ? 48 : 80, toolbar.Width);
            Assert.Equal(destination == DockSide.Top ? 80 : 48, toolbar.Height);
            Assert.True(final.Left >= monitor.WorkArea.Left && final.Right <= monitor.WorkArea.Right);
            Assert.True(final.Top >= monitor.WorkArea.Top && final.Bottom <= monitor.WorkArea.Bottom);
        });

    private static void Click(Button button) =>
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private sealed class Fixture : IDisposable
    {
        private readonly string _path;
        private readonly Window? _previousMain;
        public ToolbarWindow Toolbar { get; }
        public WindowPlacementService Placement { get; } = new();

        public Fixture(DockSide dock, TopOpeningDirection opening = TopOpeningDirection.Right)
        {
            var monitor = Placement.GetMonitors().First(candidate => candidate.IsPrimary);
            _path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            _previousMain = Application.Current.MainWindow;
            Toolbar = new ToolbarWindow(Placement, new SettingsService(_path),
                new AppSettings
                {
                    WindowPlacement = new WindowPlacement(monitor.MonitorId, dock, 100)
                    {
                        HorizontalOffset = 100,
                        TopOpeningDirection = opening
                    }
                });
            Toolbar.DataContext = new FloatingToolbarViewModel();
            Toolbar.ToolMenuRequested += (_, _) =>
            {
                var vm = (FloatingToolbarViewModel)Toolbar.DataContext;
                vm.ToggleToolMenuCommand.Execute(null);
                Toolbar.SetToolPanelVisible(vm.PanelState == PanelState.ToolMenu);
            };
            Toolbar.ToolSelected += tool =>
            {
                var vm = (FloatingToolbarViewModel)Toolbar.DataContext;
                vm.SelectToolCommand.Execute(tool);
                Toolbar.SetToolPanelVisible(false);
            };
            Toolbar.TranslationRequested += (_, _) =>
            {
                var vm = (FloatingToolbarViewModel)Toolbar.DataContext;
                vm.ToggleActiveToolPanelCommand.Execute(null);
                Toolbar.SetToolPanelVisible(false);
            };
        }

        public void Dispose()
        {
            Toolbar.Close();
            Application.Current.MainWindow = _previousMain;
            if (System.IO.File.Exists(_path)) System.IO.File.Delete(_path);
        }
    }
}
