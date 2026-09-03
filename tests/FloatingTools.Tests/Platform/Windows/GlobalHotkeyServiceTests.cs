using System.Windows.Input;
using FloatingTools.App.Platform.Windows;

namespace FloatingTools.Tests.Platform.Windows;

public sealed class GlobalHotkeyServiceTests
{
    private const int WmHotKey = 0x0312;

    [Fact]
    public void TryRegister_Succeeds_AndDispatchesToTheMatchingCallback()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        var handle = new IntPtr(12345);
        var invoked = 0;

        var registered = service.TryRegister(
            handle,
            id: 1,
            ModifierKeys.Control | ModifierKeys.Alt,
            Key.H,
            () => invoked++);

        Assert.True(registered);
        Assert.Single(nativeApi.RegisterCalls);
        Assert.Equal(handle, nativeApi.RegisterCalls[0].Handle);
        Assert.Equal(1, nativeApi.RegisterCalls[0].Id);

        var handled = service.HandleMessage(WmHotKey, new IntPtr(1));

        Assert.True(handled);
        Assert.Equal(1, invoked);
    }

    [Fact]
    public void TryRegister_ConflictingCombination_ReturnsFalseWithoutThrowing()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi { AlwaysFail = true };
        var service = new GlobalHotkeyService(nativeApi);

        var registered = service.TryRegister(
            new IntPtr(1),
            id: 1,
            ModifierKeys.Control | ModifierKeys.Alt,
            Key.H,
            () => { });

        Assert.False(registered);
    }

    [Fact]
    public void TryRegister_SameIdTwice_SecondCallIsRejectedWithoutTouchingNativeApiAgain()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        var handle = new IntPtr(1);

        var first = service.TryRegister(handle, id: 1, ModifierKeys.Control, Key.H, () => { });
        var second = service.TryRegister(handle, id: 1, ModifierKeys.Control, Key.T, () => { });

        Assert.True(first);
        Assert.False(second);
        Assert.Single(nativeApi.RegisterCalls);
    }

    [Fact]
    public void TryRegister_TwoDifferentIds_BothRegisterAndDispatchIndependently()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        var handle = new IntPtr(1);
        var showHideInvoked = 0;
        var extractTextInvoked = 0;

        service.TryRegister(handle, id: 1, ModifierKeys.Control | ModifierKeys.Alt, Key.H,
            () => showHideInvoked++);
        service.TryRegister(handle, id: 2, ModifierKeys.Control | ModifierKeys.Alt, Key.T,
            () => extractTextInvoked++);

        service.HandleMessage(WmHotKey, new IntPtr(2));

        Assert.Equal(0, showHideInvoked);
        Assert.Equal(1, extractTextInvoked);
    }

    [Fact]
    public void HandleMessage_UnknownId_ReturnsFalseAndInvokesNothing()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        service.TryRegister(new IntPtr(1), id: 1, ModifierKeys.Control, Key.H, () =>
            throw new InvalidOperationException("should not run"));

        var handled = service.HandleMessage(WmHotKey, new IntPtr(999));

        Assert.False(handled);
    }

    [Fact]
    public void HandleMessage_NonHotkeyMessage_ReturnsFalse()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        service.TryRegister(new IntPtr(1), id: 1, ModifierKeys.Control, Key.H, () =>
            throw new InvalidOperationException("should not run"));

        var handled = service.HandleMessage(0x0001, new IntPtr(1));

        Assert.False(handled);
    }

    [Fact]
    public void Dispose_UnregistersEveryRegisteredHotkey()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        var handle = new IntPtr(1);
        service.TryRegister(handle, id: 1, ModifierKeys.Control, Key.H, () => { });
        service.TryRegister(handle, id: 2, ModifierKeys.Control, Key.T, () => { });

        service.Dispose();

        Assert.Equal(2, nativeApi.UnregisterCalls.Count);
        Assert.Contains(nativeApi.UnregisterCalls, call => call.Id == 1);
        Assert.Contains(nativeApi.UnregisterCalls, call => call.Id == 2);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotDoubleUnregisterOrThrow()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        service.TryRegister(new IntPtr(1), id: 1, ModifierKeys.Control, Key.H, () => { });

        service.Dispose();
        service.Dispose();

        Assert.Single(nativeApi.UnregisterCalls);
    }

    [Fact]
    public void TryRegister_AfterDispose_ReturnsFalse()
    {
        var nativeApi = new FakeGlobalHotkeyNativeApi();
        var service = new GlobalHotkeyService(nativeApi);
        service.Dispose();

        var registered = service.TryRegister(
            new IntPtr(1), id: 1, ModifierKeys.Control, Key.H, () => { });

        Assert.False(registered);
        Assert.Empty(nativeApi.RegisterCalls);
    }

    private sealed class FakeGlobalHotkeyNativeApi : IGlobalHotkeyNativeApi
    {
        public List<(IntPtr Handle, int Id, uint Modifiers, uint VirtualKey)> RegisterCalls { get; } = [];

        public List<(IntPtr Handle, int Id)> UnregisterCalls { get; } = [];

        public bool AlwaysFail { get; init; }

        public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey)
        {
            RegisterCalls.Add((windowHandle, id, modifiers, virtualKey));
            return !AlwaysFail;
        }

        public bool UnregisterHotKey(IntPtr windowHandle, int id)
        {
            UnregisterCalls.Add((windowHandle, id));
            return true;
        }
    }
}
