using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class ToolbarTaskbarRuntimeTests
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [Fact]
    public void ProductionStyles_BlockSystemMinimize_AllowIntentionalMinimize_ProtectPlacement()
        => WpfTestApplication.Run(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings();
            var store = new SettingsService(path);
            var toolbar = new ToolbarWindow(new WindowPlacementService(), store, settings);
            try
            {
                toolbar.Show();
                var handle = new WindowInteropHelper(toolbar).Handle;
                Assert.True(toolbar.ShowInTaskbar);
                Assert.NotNull(toolbar.Icon);
                Assert.True(toolbar.Topmost);
                Assert.True(toolbar.AllowsTransparency);
                Assert.Equal(ResizeMode.NoResize, toolbar.ResizeMode);
                var changes = 0;
                toolbar.StateChanged += (_, _) => changes++;
                SendMessageW(handle, 0x0112, new IntPtr(0xF022), IntPtr.Zero);
                Drain();
                Assert.Equal(0, changes);
                Assert.Equal(WindowState.Normal, toolbar.WindowState);
                var saved = settings.WindowPlacement;
                toolbar.MinimizeUi();
                Assert.Equal(WindowState.Minimized, toolbar.WindowState);
                Assert.False(toolbar.CanUseNormalPlacement);
                typeof(ToolbarWindow).GetField("_isDragging", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(toolbar, true);
                toolbar.CompletePendingDrag();
                Assert.Equal(saved, settings.WindowPlacement);
                SendMessageW(handle, 0x0112, new IntPtr(0xF120), IntPtr.Zero);
                Drain();
                Assert.Equal(WindowState.Normal, toolbar.WindowState);
                Assert.True(toolbar.CanUseNormalPlacement);
                toolbar.MinimizeUi();
                toolbar.Close();
                Assert.Equal(saved, store.Load().WindowPlacement);
            }
            finally { if (toolbar.IsLoaded) toolbar.Close(); File.Delete(path); }
        });

    internal static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
