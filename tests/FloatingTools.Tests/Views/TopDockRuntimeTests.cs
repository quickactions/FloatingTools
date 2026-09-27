using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.Views;
using FloatingTools.Tests.ViewModels;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class TopDockRuntimeTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    private const int DwmWindowCornerPreference = 33;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr window, int attribute, out int value, int valueSize);

    private static void AssertDwmCornerPreference(ToolbarWindow toolbar, DockSide dock)
    {
        Assert.Equal(0, toolbar.DwmCornerPreferenceHResult);
        var result = DwmGetWindowAttribute(
            new WindowInteropHelper(toolbar).Handle,
            DwmWindowCornerPreference, out var preference, sizeof(int));
        Assert.Equal(0, result);
        Assert.Equal(dock == DockSide.Top ? 1 : 0, preference);
    }
    [Theory]
    [InlineData(DockSide.Top, 48, 80, 48, 48, 48, 32, 1)]
    [InlineData(DockSide.Left, 80, 48, 48, 48, 32, 48, 0)]
    [InlineData(DockSide.Right, 80, 48, 48, 48, 32, 48, 0)]
    public void Toolbar_PreservesPhysicalButtonGeometry(
        DockSide dock, double width, double height,
        double cubeWidth, double cubeHeight, double dotsWidth, double dotsHeight, int dotsRow)
        => WpfTestApplication.Run(() =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement("DISPLAY1", dock, 0)
            };
            var toolbar = new ToolbarWindow(
                new WindowPlacementService(), new SettingsService(path), settings);
            try
            {
                var cube = (Button)toolbar.FindName("MainTileButton");
                var dots = (Button)toolbar.FindName("ToolsMenuButton");
                Assert.Equal(width, toolbar.Width);
                Assert.Equal(height, toolbar.Height);
                Assert.Equal(cubeWidth, cube.Width);
                Assert.Equal(cubeHeight, cube.Height);
                Assert.Equal(dotsWidth, dots.Width);
                Assert.Equal(dotsHeight, dots.Height);
                Assert.Equal(dotsRow, Grid.GetRow(dots));
                var glyph = (TextBlock)toolbar.FindName("ToolsMenuGlyph");
                var topDots = (StackPanel)toolbar.FindName("TopDockDots");
                Assert.Equal("\u22EE", glyph.Text);
                Assert.Equal(dock == DockSide.Top ? Visibility.Collapsed : Visibility.Visible, glyph.Visibility);
                Assert.Equal(dock == DockSide.Top ? Visibility.Visible : Visibility.Collapsed, topDots.Visibility);
                Assert.Equal(0, Grid.GetRow((Grid)toolbar.FindName("CubeShadowClip")));
            }
            finally
            {
                toolbar.Close();
                System.IO.File.Delete(path);
            }
        });

    [Theory]
    [InlineData(DockSide.Top)]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    public void Toolbar_RequestsNativeCornerPreferenceForCurrentDock(DockSide dock)
        => WpfTestApplication.Run(() =>
        {
            var placement = new WindowPlacementService();
            var monitor = placement.GetMonitors().First(candidate => candidate.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement(monitor.MonitorId, dock, 0)
            };
            var toolbar = new ToolbarWindow(placement, new SettingsService(path), settings);
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                toolbar.Show();
                AssertDwmCornerPreference(toolbar, dock);
                Assert.Equal(dock == DockSide.Top ? 48 : 80, toolbar.Width);
                Assert.Equal(dock == DockSide.Top ? 80 : 48, toolbar.Height);
            }
            finally
            {
                toolbar.Close();
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Fact]
    public void TopDots_EntireIdleRowBelongsToToolbarNativeHitTarget()
        => WpfTestApplication.Run(() =>
        {
            var placement = new WindowPlacementService();
            var monitor = placement.GetMonitors().First(candidate => candidate.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement(monitor.MonitorId, DockSide.Top, 0)
                {
                    HorizontalOffset = Math.Max(0, monitor.WorkArea.Width / monitor.DpiScale / 2 - 24)
                }
            };
            var toolbar = new ToolbarWindow(placement, new SettingsService(path), settings);
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                toolbar.Show();
                toolbar.UpdateLayout();
                // Allow the layered HWND to present before sampling native pixels.
                var presentationFrame = new DispatcherFrame();
                var presentationTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(350)
                };
                presentationTimer.Tick += (_, _) =>
                {
                    presentationTimer.Stop();
                    presentationFrame.Continue = false;
                };
                presentationTimer.Start();
                Dispatcher.PushFrame(presentationFrame);
                var hwnd = new WindowInteropHelper(toolbar).Handle;
                var button = (Button)toolbar.FindName("ToolsMenuButton");
                var underlay = (Border)toolbar.FindName("TopDockDotsHitSurface");
                Assert.Equal(48, underlay.ActualWidth);
                Assert.Equal(32, underlay.ActualHeight);
                Assert.Equal(1, Assert.IsType<SolidColorBrush>(underlay.Background).Color.A);
                Assert.False(underlay.IsHitTestVisible);
                Assert.Equal(Visibility.Visible, underlay.Visibility);
                Assert.Equal(Visibility.Visible, ((StackPanel)toolbar.FindName("TopDockDots")).Visibility);
                Assert.Equal(System.Windows.Input.Cursors.Hand, button.Cursor);

                foreach (var (x, y) in new[]
                {
                    (3, 64), (20, 64), (24, 64), (45, 64), (24, 50), (24, 78)
                })
                {
                    var screen = toolbar.PointToScreen(new Point(x, y));
                    var nativeHit = WindowFromPoint(new NativePoint
                    {
                        X = (int)Math.Round(screen.X),
                        Y = (int)Math.Round(screen.Y)
                    });
                    Assert.Equal(hwnd, nativeHit);
                    var wpfHit = Assert.IsAssignableFrom<DependencyObject>(
                        toolbar.InputHitTest(new Point(x, y)));
                    DependencyObject? current = wpfHit;
                    while (current is not null && !ReferenceEquals(button, current))
                    {
                        current = VisualTreeHelper.GetParent(current);
                    }
                    Assert.Same(button, current);
                }

                var menuRequests = 0;
                toolbar.ToolMenuRequested += (_, _) => menuRequests++;
                button.RaiseEvent(new RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(1, menuRequests);
                Assert.Equal(48, toolbar.Width);
                Assert.Equal(80, toolbar.Height);
            }
            finally
            {
                toolbar.Close();
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Fact]
    public void TopDots_AreThreeCenteredThemeColoredCircles()
        => WpfTestApplication.Run(() =>
        {
            var placement = new WindowPlacementService();
            var monitor = placement.GetMonitors().First(candidate => candidate.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement(monitor.MonitorId, DockSide.Top, 0)
            };
            var toolbar = new ToolbarWindow(placement, new SettingsService(path), settings);
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                toolbar.Show();
                toolbar.UpdateLayout();
                var button = (Button)toolbar.FindName("ToolsMenuButton");
                var dots = (StackPanel)toolbar.FindName("TopDockDots");
                Assert.Equal(48, button.ActualWidth);
                Assert.Equal(32, button.ActualHeight);
                Assert.Equal(48, toolbar.ActualWidth);
                Assert.Equal(80, toolbar.ActualHeight);
                Assert.Equal(Visibility.Visible, dots.Visibility);
                Assert.Equal(Orientation.Horizontal, dots.Orientation);
                Assert.Equal(3, dots.Children.Count);
                foreach (var child in dots.Children)
                {
                    var circle = Assert.IsType<Ellipse>(child);
                    Assert.Equal(2.5, circle.Width);
                    Assert.Equal(2.5, circle.Height);
                    Assert.InRange(circle.ActualWidth, 2.3, 2.7);
                    Assert.InRange(circle.ActualHeight, 2.3, 2.7);
                    Assert.Equal(
                        ((SolidColorBrush)toolbar.FindResource("FloatingToolsBrushForegroundPrimary")).Color,
                        Assert.IsType<SolidColorBrush>(circle.Fill).Color);
                }
                Assert.Equal(new Thickness(0, 0, 4.25, 0), ((Ellipse)dots.Children[0]).Margin);
                Assert.Equal(new Thickness(0, 0, 4.25, 0), ((Ellipse)dots.Children[1]).Margin);
                Assert.Equal(new Thickness(0), ((Ellipse)dots.Children[2]).Margin);
                var upperLeft = dots.TranslatePoint(new Point(0, 0), button);
                Assert.InRange(Math.Abs(upperLeft.X + dots.ActualWidth / 2 - 24), 0, 0.5);
                Assert.InRange(Math.Abs(upperLeft.Y + dots.ActualHeight / 2 - 16), 0, 0.5);
            }
            finally
            {
                toolbar.Close();
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Theory]
    [InlineData(DockSide.Top, DockSide.Right)]
    [InlineData(DockSide.Top, DockSide.Left)]
    [InlineData(DockSide.Left, DockSide.Right)]
    [InlineData(DockSide.Right, DockSide.Left)]
    [InlineData(DockSide.Top, DockSide.Top)]
    public void DragSnap_UsesDestinationToolbarSizeBeforeEdgePlacement(
        DockSide initial, DockSide destination)
        => WpfTestApplication.Run(() =>
        {
            var placementService = new WindowPlacementService();
            var monitor = placementService.GetMonitors().First(candidate => candidate.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement(monitor.MonitorId, initial, 100)
                {
                    HorizontalOffset = 100,
                    TopOpeningDirection = TopOpeningDirection.Right
                }
            };
            var toolbar = new ToolbarWindow(
                placementService, new SettingsService(path), settings);
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                toolbar.Show();
                var handle = new WindowInteropHelper(toolbar).Handle;
                var initialBounds = placementService.GetWindowBounds(handle);
                var scale = monitor.DpiScale;
                Assert.InRange(Math.Abs(initialBounds.Width
                    - (initial == DockSide.Top ? 48 : 80) * scale), 0, 1);
                Assert.InRange(Math.Abs(initialBounds.Height
                    - (initial == DockSide.Top ? 80 : 48) * scale), 0, 1);

                var rawLeft = destination switch
                {
                    DockSide.Right => monitor.WorkArea.Right - initialBounds.Width,
                    DockSide.Left => monitor.WorkArea.Left,
                    _ => monitor.WorkArea.Left
                        + (monitor.WorkArea.Width - initialBounds.Width) / 2
                };
                var rawTop = destination == DockSide.Top
                    ? monitor.WorkArea.Top
                    : monitor.WorkArea.Top + Math.Min(100,
                        Math.Max(0, monitor.WorkArea.Height - initialBounds.Height));
                placementService.MoveWindow(handle, rawLeft, rawTop);

                var snapped = toolbar.SnapToNearestDock();
                var final = placementService.GetWindowBounds(handle);
                var expectedWidth = destination == DockSide.Top ? 48 : 80;
                var expectedHeight = destination == DockSide.Top ? 80 : 48;
                Assert.Equal(destination, snapped.DockSide);
                AssertDwmCornerPreference(toolbar, destination);
                Assert.Equal(destination, settings.WindowPlacement?.DockSide);
                Assert.Equal(expectedWidth, toolbar.Width);
                Assert.Equal(expectedHeight, toolbar.Height);
                Assert.InRange(Math.Abs(final.Width - expectedWidth * scale), 0, 1);
                Assert.InRange(Math.Abs(final.Height - expectedHeight * scale), 0, 1);
                Assert.True(final.Left >= monitor.WorkArea.Left);
                Assert.True(final.Right <= monitor.WorkArea.Right);
                Assert.True(final.Top >= monitor.WorkArea.Top);
                Assert.True(final.Bottom <= monitor.WorkArea.Bottom);
                if (destination == DockSide.Right)
                {
                    Assert.Equal(monitor.WorkArea.Right, final.Right);
                    Assert.Equal(1, Grid.GetColumn((Grid)toolbar.FindName("CubeShadowClip")));
                    Assert.Equal(0, Grid.GetColumn((Button)toolbar.FindName("ToolsMenuButton")));
                }
                else if (destination == DockSide.Left)
                {
                    Assert.Equal(monitor.WorkArea.Left, final.Left);
                }
                else
                {
                    Assert.Equal(monitor.WorkArea.Top, final.Top);
                }
            }
            finally
            {
                toolbar.Close();
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Fact]
    public void TopPlacement_RestoresToolbarGeometryAndDirectionAfterRestart()
        => WpfTestApplication.Run(() =>
        {
            var placement = new WindowPlacementService();
            var monitor = placement.GetMonitors().First(candidate => candidate.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var store = new SettingsService(path);
            var offset = Math.Min(120, Math.Max(0, (monitor.WorkArea.Width - 80) / monitor.DpiScale));
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement(monitor.MonitorId, DockSide.Top, 0)
                {
                    HorizontalOffset = offset,
                    TopOpeningDirection = TopOpeningDirection.Left
                }
            };
            Assert.True(store.Save(settings));
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var toolbar = new ToolbarWindow(placement, store, store.Load());
                    try
                    {
                        toolbar.Show();
                        toolbar.UpdateLayout();
                        var bounds = placement.GetWindowBounds(new WindowInteropHelper(toolbar).Handle);
                        Assert.Equal(monitor.WorkArea.Top, bounds.Top);
                        Assert.InRange(Math.Abs(bounds.Left - monitor.WorkArea.Left
                            - offset * monitor.DpiScale), 0, 1);
                        Assert.InRange(Math.Abs(bounds.Width - 48 * monitor.DpiScale), 0, 1);
                        Assert.InRange(Math.Abs(bounds.Height - 80 * monitor.DpiScale), 0, 1);
                    }
                    finally { toolbar.Close(); }
                    Assert.Equal(DockSide.Top, store.Load().WindowPlacement?.DockSide);
                    Assert.Equal(TopOpeningDirection.Left,
                        store.Load().WindowPlacement?.TopOpeningDirection);
                }
            }
            finally
            {
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Fact]
    public void MissingTopMonitor_FallsBackToVisibleRightSideToolbar()
        => WpfTestApplication.Run(() =>
        {
            var placement = new WindowPlacementService();
            var primary = placement.GetMonitors().First(monitor => monitor.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement("MISSING_TOP_MONITOR", DockSide.Top, 0)
                {
                    HorizontalOffset = 500,
                    TopOpeningDirection = TopOpeningDirection.Left
                }
            };
            var toolbar = new ToolbarWindow(placement, new SettingsService(path), settings);
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                toolbar.Show();
                toolbar.UpdateLayout();
                var bounds = placement.GetWindowBounds(new WindowInteropHelper(toolbar).Handle);
                Assert.Equal(DockSide.Right, settings.WindowPlacement?.DockSide);
                Assert.InRange(Math.Abs(bounds.Width - 80 * primary.DpiScale), 0, 1);
                Assert.Equal(primary.WorkArea.Right, bounds.Right);
            }
            finally
            {
                toolbar.Close();
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Fact]
    public void TopDots_TemplateSurfaceIsSquareAndTransparentAtIdle()
        => WpfTestApplication.Run(() =>
        {
            var placement = new WindowPlacementService();
            var monitor = placement.GetMonitors().First(candidate => candidate.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement(monitor.MonitorId, DockSide.Top, 0)
            };
            var toolbar = new ToolbarWindow(placement, new SettingsService(path), settings);
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                toolbar.Show();
                toolbar.UpdateLayout();
                var dots = (Button)toolbar.FindName("ToolsMenuButton");
                var surface = (Border)dots.Template.FindName("ButtonBorder", dots);
                Assert.Equal(DockSide.Top, settings.WindowPlacement?.DockSide);
                Assert.Equal(48, toolbar.Width);
                Assert.Equal(80, toolbar.Height);
                Assert.Same(surface, System.Windows.Media.VisualTreeHelper.GetChild(dots, 0));
                Assert.Null(((Grid)toolbar.FindName("ToolbarBlock")).Background);
                Assert.Null(((Grid)toolbar.FindName("ShellRoot")).Background);
                Assert.Equal(new CornerRadius(0), surface.CornerRadius);
                var underlay = (Border)toolbar.FindName("TopDockDotsHitSurface");
                Assert.Equal(Visibility.Visible, underlay.Visibility);
                Assert.Equal(1, Assert.IsType<SolidColorBrush>(underlay.Background).Color.A);
                var shell = (Grid)toolbar.FindName("ShellRoot");
                var cube = (Button)toolbar.FindName("MainTileButton");
                var cubeClip = (Grid)toolbar.FindName("CubeShadowClip");
                Assert.Null(shell.Effect);
                Assert.True(cubeClip.ClipToBounds);
                var cubeShadow = Assert.IsType<DropShadowEffect>(cube.Effect);
                Assert.Equal(8, cubeShadow.BlurRadius);
                Assert.Equal(1, cubeShadow.ShadowDepth);
                Assert.Equal(0.13, cubeShadow.Opacity);
                var idleBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    48, 80, 96, 96, PixelFormats.Pbgra32);
                idleBitmap.Render((Visual)toolbar.FindName("ToolbarBlock"));
                var idlePixels = new byte[48 * 80 * 4];
                idleBitmap.CopyPixels(idlePixels, 48 * 4, 0);
                foreach (var x in new[] { 0, 24, 47 })
                {
                    for (var y = 48; y <= 53; y++)
                    {
                        Assert.Equal(1, idlePixels[((y * 48) + x) * 4 + 3]);
                    }
                }

                // The idle button remains transparent; use a test-only fill to verify the rendered rectangle.
                Assert.Equal(0, Assert.IsType<System.Windows.Media.SolidColorBrush>(surface.Background).Color.A);
                surface.Background = System.Windows.Media.Brushes.Magenta;
                toolbar.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    48, 80, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render((System.Windows.Media.Visual)toolbar.FindName("ToolbarBlock"));
                var pixels = new byte[48 * 80 * 4];
                bitmap.CopyPixels(pixels, 48 * 4, 0);
                byte AlphaAt(int x, int y) => pixels[((y * 48) + x) * 4 + 3];
                Assert.True(AlphaAt(1, 49) > 0);
                Assert.True(AlphaAt(46, 49) > 0);
                Assert.True(AlphaAt(1, 78) > 0);
                Assert.True(AlphaAt(46, 78) > 0);
                Assert.True(AlphaAt(24, 78) > 0);
                var menuRequests = 0;
                toolbar.ToolMenuRequested += (_, _) => menuRequests++;
                dots.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(1, menuRequests);
                surface.ClearValue(Border.BackgroundProperty);
                toolbar.DataContext = new FloatingTools.App.ViewModels.FloatingToolbarViewModel
                {
                    PanelState = PanelState.ToolMenu
                };
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                var overlay = Assert.IsType<System.Windows.Media.SolidColorBrush>(
                    toolbar.FindResource("FloatingToolsBrushOverlayHover"));
                Assert.Equal(overlay.Color,
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(surface.Background).Color);
            }
            finally
            {
                toolbar.Close();
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Theory]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Right)]
    public void SideDots_RetainTransparentSquareSurface(DockSide dock)
        => WpfTestApplication.Run(() =>
        {
            var placement = new WindowPlacementService();
            var monitor = placement.GetMonitors().First(candidate => candidate.IsPrimary);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement(monitor.MonitorId, dock, 0)
            };
            var toolbar = new ToolbarWindow(placement, new SettingsService(path), settings);
            var originalMainWindow = Application.Current.MainWindow;
            try
            {
                toolbar.Show();
                var dots = (Button)toolbar.FindName("ToolsMenuButton");
                var surface = (Border)dots.Template.FindName("ButtonBorder", dots);
                Assert.Equal(dock, settings.WindowPlacement?.DockSide);
                Assert.Equal(new CornerRadius(0), surface.CornerRadius);
                Assert.Equal(0, Assert.IsType<System.Windows.Media.SolidColorBrush>(surface.Background).Color.A);
                Assert.IsType<DropShadowEffect>(((Grid)toolbar.FindName("ShellRoot")).Effect);
                Assert.Null(((Button)toolbar.FindName("MainTileButton")).Effect);
                Assert.False(((Grid)toolbar.FindName("CubeShadowClip")).ClipToBounds);
                Assert.Equal(Visibility.Collapsed,
                    ((Border)toolbar.FindName("TopDockDotsHitSurface")).Visibility);
                var cube = (Button)toolbar.FindName("MainTileButton");
                var cubeBorder = (Border)cube.Template.FindName("ButtonBorder", cube);
                Assert.Equal(dock == DockSide.Right
                    ? new CornerRadius(12, 0, 0, 12)
                    : new CornerRadius(0, 12, 12, 0), cubeBorder.CornerRadius);
                Assert.Equal(Visibility.Visible, ((TextBlock)toolbar.FindName("ToolsMenuGlyph")).Visibility);
                Assert.Equal(Visibility.Collapsed, ((StackPanel)toolbar.FindName("TopDockDots")).Visibility);
            }
            finally
            {
                toolbar.Close();
                Application.Current.MainWindow = originalMainWindow;
                System.IO.File.Delete(path);
            }
        });

    [Theory]
    [InlineData(TopOpeningDirection.Right)]
    [InlineData(TopOpeningDirection.Left)]
    public void TopToolbar_RendersCubeAndDotsSquare(
        TopOpeningDirection opening)
        => WpfTestApplication.Run(() =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings
            {
                WindowPlacement = new WindowPlacement("DISPLAY1", DockSide.Top, 0)
                {
                    TopOpeningDirection = opening
                }
            };
            var toolbar = new ToolbarWindow(
                new WindowPlacementService(), new SettingsService(path), settings);
            try
            {
                toolbar.UpdatePanelConnection(true);
                var cube = (Button)toolbar.FindName("MainTileButton");
                var dots = (Button)toolbar.FindName("ToolsMenuButton");
                var cubeBorder = (Border)cube.Template.FindName("ButtonBorder", cube);
                var dotsBorder = (Border)dots.Template.FindName("ButtonBorder", dots);
                Assert.Equal(new CornerRadius(0, 0, 12, 12), cubeBorder.CornerRadius);
                Assert.Same(cubeBorder, VisualTreeHelper.GetChild(cube, 0));
                Assert.Equal(48, toolbar.Width);
                Assert.Equal(80, toolbar.Height);
                Assert.Equal(Visibility.Visible, ((StackPanel)toolbar.FindName("TopDockDots")).Visibility);
                Assert.Equal(0, dotsBorder.CornerRadius.TopLeft);
                Assert.Equal(0, dotsBorder.CornerRadius.TopRight);
                Assert.Equal(0, dotsBorder.CornerRadius.BottomLeft);
                Assert.Equal(0, dotsBorder.CornerRadius.BottomRight);
            }
            finally
            {
                toolbar.Close();
                System.IO.File.Delete(path);
            }
        });

    [Theory]
    [InlineData(PanelSizePreset.Standard, 460, 258, 212, 412, 92)]
    [InlineData(PanelSizePreset.Large, 720, 518, 472, 672, 180)]
    public void TopCalendar_ScrollsMonthWhileSelectedDayStaysFixed(
        PanelSizePreset preset, double width, double height, double bodyHeight,
        double minimumMonthHeight, double dayMinimum)
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
            viewModel.SelectDateCommand.Execute(new DateOnly(2026, 9, 8));
            var view = new CalendarToolView { DataContext = viewModel };
            view.SetTopDocked(true, preset);
            var window = new Window
            {
                Content = view, Width = width, Height = height,
                Left = -10000, Top = -10000, ShowInTaskbar = false,
                ShowActivated = false, WindowStyle = WindowStyle.None
            };
            try
            {
                window.Show();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                view.UpdateLayout();

                var scroller = (ScrollViewer)view.FindName("TopDockCalendarScrollViewer");
                var monthScroller = (ScrollViewer)view.FindName("MonthScrollViewer");
                var weekScroller = (ScrollViewer)view.FindName("WeekScrollViewer");
                var dayScroller = (ScrollViewer)view.FindName("DayPanelScrollViewer");
                var body = (Grid)view.FindName("CalendarBody");
                var monthContent = (Grid)view.FindName("CalendarMainContent");
                var dayPanel = (Grid)view.FindName("DayPanelOverlay");
                var dayContent = (Border)view.FindName("DayPanelExpandedContent");
                Assert.Equal(ScrollBarVisibility.Auto, scroller.VerticalScrollBarVisibility);
                Assert.Same(view.FindResource("FloatingToolsSharedScrollViewerStyle"), scroller.Style);
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("MonthOverlayInset")).Visibility);
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("WeekOverlayInset")).Visibility);
                Assert.Equal(1, Grid.GetRowSpan(scroller));
                Assert.Equal(1, Grid.GetRow(dayPanel));
                Assert.Equal(1, Grid.GetRowSpan(dayPanel));
                Assert.Same(body, dayPanel.Parent);
                Assert.Equal(bodyHeight, body.MinHeight);
                Assert.Equal(minimumMonthHeight, monthContent.MinHeight);
                Assert.Equal(dayMinimum, dayContent.MinHeight);
                Assert.True(scroller.ScrollableHeight > 100);
                Assert.Equal(ScrollBarVisibility.Disabled, monthScroller.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, weekScroller.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Auto, dayScroller.VerticalScrollBarVisibility);

                var dayTop = dayPanel.TransformToAncestor(body).Transform(new Point()).Y;
                scroller.ScrollToBottom();
                view.UpdateLayout();
                Assert.InRange(dayPanel.TransformToAncestor(body).Transform(new Point()).Y,
                    dayTop - 1, dayTop + 1);
                Assert.InRange(dayPanel.TransformToAncestor(body)
                    .Transform(new Point(0, dayPanel.ActualHeight)).Y,
                    0, body.ActualHeight + 1);
                Assert.Equal(new DateOnly(2026, 9, 8), viewModel.SelectedDate);

                view.SetTopDocked(false, preset);
                view.UpdateLayout();
                Assert.Equal(ScrollBarVisibility.Disabled, scroller.VerticalScrollBarVisibility);
                Assert.Equal(2, Grid.GetRowSpan(scroller));
                Assert.Equal(0, Grid.GetRow(dayPanel));
                Assert.Equal(2, Grid.GetRowSpan(dayPanel));
                Assert.Equal(ScrollBarVisibility.Auto, monthScroller.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Auto, weekScroller.VerticalScrollBarVisibility);
                Assert.Equal(Visibility.Visible, ((Border)view.FindName("MonthOverlayInset")).Visibility);
                Assert.Equal(Visibility.Visible, ((Border)view.FindName("WeekOverlayInset")).Visibility);
                Assert.Equal(0, body.MinHeight);
                Assert.True(double.IsNaN(body.Height));
            }
            finally { window.Close(); }
        });
}