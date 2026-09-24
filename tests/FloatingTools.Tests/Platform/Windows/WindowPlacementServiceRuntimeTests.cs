using System.Windows;
using System.Windows.Interop;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;

namespace FloatingTools.Tests.Platform.Windows;

[Collection(WpfResourceCollection.Name)]
public sealed class WindowPlacementServiceRuntimeTests
{
    [Fact]
    public void RequiresConnectedPlacement_ReturnsFalseForIdenticalFinalBounds()
    {
        Assert.False(WindowPlacementService.RequiresConnectedPlacement(
            new PixelRect(0, 100, 80, 148),
            new PixelPoint(0, 100),
            new PixelRect(0, 148, 420, 832),
            new PixelPoint(0, 148),
            panelHostWidth: 420,
            panelHostHeight: 684));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RequiresConnectedPlacement_DetectsToolbarOrHostChanges(
        bool moveToolbar,
        bool resizeHost)
    {
        Assert.True(WindowPlacementService.RequiresConnectedPlacement(
            new PixelRect(0, 100, 80, 148),
            new PixelPoint(0, moveToolbar ? 90 : 100),
            new PixelRect(0, 148, 420, 832),
            new PixelPoint(0, 148),
            panelHostWidth: resizeHost ? 560 : 420,
            panelHostHeight: 684));
    }
    [Fact]
    public void PlaceConnectedWindows_AppliesFinalToolbarAndPanelBoundsTogether()
        => WpfTestApplication.Run(() =>
        {
            var service = new WindowPlacementService();
            var toolbar = CreateWindow(80, 48);
            var panel = CreateWindow(300, 500);

            try
            {
                toolbar.Show();
                panel.Show();
                var toolbarHandle = new WindowInteropHelper(toolbar).EnsureHandle();
                var panelHandle = new WindowInteropHelper(panel).EnsureHandle();
                var originalToolbarBounds = service.GetWindowBounds(toolbarHandle);
                var toolbarPosition = new PixelPoint(-9000, -9000);
                var panelPosition = new PixelPoint(
                    toolbarPosition.X,
                    toolbarPosition.Y + originalToolbarBounds.Height);

                service.PlaceConnectedWindows(
                    toolbarHandle,
                    toolbarPosition,
                    panelHandle,
                    panelPosition,
                    panelWidth: 420,
                    panelHeight: 684);

                var toolbarBounds = service.GetWindowBounds(toolbarHandle);
                var panelBounds = service.GetWindowBounds(panelHandle);
                Assert.Equal(toolbarPosition.X, toolbarBounds.Left);
                Assert.Equal(toolbarPosition.Y, toolbarBounds.Top);
                Assert.Equal(originalToolbarBounds.Width, toolbarBounds.Width);
                Assert.Equal(originalToolbarBounds.Height, toolbarBounds.Height);
                Assert.Equal(panelPosition.X, panelBounds.Left);
                Assert.Equal(panelPosition.Y, panelBounds.Top);
                Assert.Equal(420, panelBounds.Width);
                Assert.Equal(684, panelBounds.Height);
                Assert.Equal(toolbarBounds.Bottom, panelBounds.Top);
            }
            finally
            {
                panel.Close();
                toolbar.Close();
            }
        });

    private static Window CreateWindow(double width, double height) => new()
    {
        Width = width,
        Height = height,
        Left = -10000,
        Top = -10000,
        WindowStartupLocation = WindowStartupLocation.Manual,
        WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false,
        ShowActivated = false
    };
}