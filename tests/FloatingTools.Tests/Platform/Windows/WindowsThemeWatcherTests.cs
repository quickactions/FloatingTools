using System.Runtime.InteropServices;
using FloatingTools.App.Platform.Windows;

namespace FloatingTools.Tests.Platform.Windows;

public sealed class WindowsThemeWatcherTests
{
    private const int WmSettingChange = 0x001A;
    private const int WmOtherMessage = 0x0001;

    [Fact]
    public void HandleMessage_ImmersiveColorSetSetting_RaisesThemeChanged()
    {
        var watcher = new WindowsThemeWatcher();
        var raised = 0;
        watcher.ThemeChanged += (_, _) => raised++;

        WithMarshaledString("ImmersiveColorSet", lParam =>
        {
            var handled = watcher.HandleMessage(WmSettingChange, lParam);
            Assert.True(handled);
        });

        Assert.Equal(1, raised);
    }

    [Fact]
    public void HandleMessage_UnrelatedSettingName_DoesNotRaiseThemeChanged()
    {
        var watcher = new WindowsThemeWatcher();
        var raised = 0;
        watcher.ThemeChanged += (_, _) => raised++;

        WithMarshaledString("Environment", lParam =>
        {
            var handled = watcher.HandleMessage(WmSettingChange, lParam);
            Assert.False(handled);
        });

        Assert.Equal(0, raised);
    }

    [Fact]
    public void HandleMessage_NonSettingChangeMessage_IsIgnoredEvenWithMatchingLParam()
    {
        var watcher = new WindowsThemeWatcher();
        var raised = 0;
        watcher.ThemeChanged += (_, _) => raised++;

        WithMarshaledString("ImmersiveColorSet", lParam =>
        {
            var handled = watcher.HandleMessage(WmOtherMessage, lParam);
            Assert.False(handled);
        });

        Assert.Equal(0, raised);
    }

    [Fact]
    public void HandleMessage_NullLParam_IsIgnored()
    {
        var watcher = new WindowsThemeWatcher();
        var raised = 0;
        watcher.ThemeChanged += (_, _) => raised++;

        var handled = watcher.HandleMessage(WmSettingChange, IntPtr.Zero);

        Assert.False(handled);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Start_WithInvalidHandle_DoesNotThrow()
    {
        var watcher = new WindowsThemeWatcher();

        watcher.Start(IntPtr.Zero);
        watcher.Start(new IntPtr(-1));
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var watcher = new WindowsThemeWatcher();

        watcher.Dispose();
        watcher.Dispose();
    }

    [Fact]
    public void Dispose_ThenHandleMessage_StillSafeAndDoesNotThrow()
    {
        var watcher = new WindowsThemeWatcher();
        var raised = 0;
        watcher.ThemeChanged += (_, _) => raised++;

        watcher.Dispose();

        WithMarshaledString("ImmersiveColorSet", lParam =>
            watcher.HandleMessage(WmSettingChange, lParam));

        // HandleMessage doesn't depend on hook state, so the event can still
        // fire post-Dispose for a directly-invoked call; what matters is that
        // no exception is thrown and the native hook itself was released.
        Assert.Equal(1, raised);
    }

    private static void WithMarshaledString(string value, Action<IntPtr> useValue)
    {
        var pointer = Marshal.StringToHGlobalUni(value);
        try
        {
            useValue(pointer);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
