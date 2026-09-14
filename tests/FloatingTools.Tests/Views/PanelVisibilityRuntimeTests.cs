using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Regression coverage for the reported "hide/show returns to Translation"
/// behavior. Hiding is temporary visibility only: it must not disturb
/// PanelState/ActiveTool, and the same tool view must still be the visible one
/// after the panel window is shown again. The window-level style triggers that
/// pick the active tool view are re-evaluated by WPF on Show, so this is
/// exercised against a real, laid-out PanelWindow rather than the view model
/// alone.
/// </summary>
[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class PanelVisibilityRuntimeTests
{
    [Theory]
    [InlineData(ToolId.QuickChat)]
    [InlineData(ToolId.Calendar)]
    [InlineData(ToolId.Notes)]
    public void HideThenShow_KeepsTheSameToolViewVisible(ToolId tool)
        => WpfTestApplication.Run(() =>
        {
            var toolbarViewModel = new FloatingToolbarViewModel(tool);
            toolbarViewModel.SelectToolCommand.Execute(tool);
            using var fixture = PanelFixture.Create(toolbarViewModel);

            Assert.Equal(tool, toolbarViewModel.ActiveTool);
            Assert.Equal(PanelState.ActiveTool, toolbarViewModel.PanelState);
            Assert.Equal(Visibility.Visible, fixture.VisibilityOf(tool));

            fixture.Window.Hide();
            fixture.Window.UpdateLayout();
            fixture.Window.Show();
            fixture.Window.UpdateLayout();

            // The tool must survive a hide/show round trip untouched.
            Assert.Equal(tool, toolbarViewModel.ActiveTool);
            Assert.Equal(PanelState.ActiveTool, toolbarViewModel.PanelState);
            Assert.Equal(Visibility.Visible, fixture.VisibilityOf(tool));

            foreach (var other in new[]
                     {
                         ToolId.Translation, ToolId.Notes,
                         ToolId.QuickChat, ToolId.Calendar
                     })
            {
                if (other != tool)
                {
                    Assert.Equal(Visibility.Collapsed, fixture.VisibilityOf(other));
                }
            }
        });

    [Fact]
    public void ToolViews_AreNotConstructedUntilTheirToolIsFirstOpened()
        => WpfTestApplication.Run(() =>
        {
            var toolbarViewModel = new FloatingToolbarViewModel(ToolId.Translation);
            toolbarViewModel.SelectToolCommand.Execute(ToolId.Translation);
            using var fixture = PanelFixture.Create(toolbarViewModel);

            // Only the tool actually shown has been built.
            Assert.NotNull(fixture.HostContent(ToolId.Translation));
            Assert.Null(fixture.HostContent(ToolId.Notes));
            Assert.Null(fixture.HostContent(ToolId.QuickChat));
            Assert.Null(fixture.HostContent(ToolId.Calendar));
            Assert.Null(fixture.SettingsContent());

            toolbarViewModel.SelectToolCommand.Execute(ToolId.Calendar);
            fixture.Window.UpdateLayout();

            Assert.NotNull(fixture.HostContent(ToolId.Calendar));
            Assert.Null(fixture.HostContent(ToolId.Notes));
            Assert.Null(fixture.HostContent(ToolId.QuickChat));
        });

    [Fact]
    public void ReopeningATool_ReusesTheSameViewInstanceSoItsStateSurvives()
        => WpfTestApplication.Run(() =>
        {
            var toolbarViewModel = new FloatingToolbarViewModel(ToolId.Notes);
            toolbarViewModel.SelectToolCommand.Execute(ToolId.Notes);
            using var fixture = PanelFixture.Create(toolbarViewModel);
            var firstInstance = fixture.HostContent(ToolId.Notes);
            Assert.NotNull(firstInstance);

            toolbarViewModel.SelectToolCommand.Execute(ToolId.Calendar);
            fixture.Window.UpdateLayout();
            toolbarViewModel.SelectToolCommand.Execute(ToolId.Notes);
            fixture.Window.UpdateLayout();

            // Same instance: the tool is created at most once per session, so
            // its view state (scroll position, selection, drafts) is preserved.
            Assert.Same(firstInstance, fixture.HostContent(ToolId.Notes));
        });

    [Fact]
    public void SettingsView_IsBuiltOnlyWhenTheSettingsPageIsFirstOpened()
        => WpfTestApplication.Run(() =>
        {
            var toolbarViewModel = new FloatingToolbarViewModel(ToolId.Translation);
            toolbarViewModel.SelectToolCommand.Execute(ToolId.Translation);
            using var fixture = PanelFixture.Create(toolbarViewModel);

            Assert.Null(fixture.SettingsContent());

            toolbarViewModel.OpenApplicationSettingsCommand.Execute(null);
            fixture.Window.UpdateLayout();

            var settings = fixture.SettingsContent();
            Assert.NotNull(settings);

            // Returning to a tool and back keeps the same settings instance.
            toolbarViewModel.SelectToolCommand.Execute(ToolId.Translation);
            fixture.Window.UpdateLayout();
            toolbarViewModel.OpenApplicationSettingsCommand.Execute(null);
            fixture.Window.UpdateLayout();
            Assert.Same(settings, fixture.SettingsContent());
        });

    [Theory]
    [InlineData(ToolId.Translation)]
    [InlineData(ToolId.Notes)]
    [InlineData(ToolId.QuickChat)]
    [InlineData(ToolId.Calendar)]
    public void SelectingEachTool_BuildsAndShowsThatToolExactlyAsBefore(ToolId tool)
        => WpfTestApplication.Run(() =>
        {
            // Mirrors the Ctrl+1..Ctrl+4 key bindings, which route through
            // SelectToolCommand with the same parameters.
            var toolbarViewModel = new FloatingToolbarViewModel(ToolId.Translation);
            toolbarViewModel.SelectToolCommand.Execute(ToolId.Translation);
            using var fixture = PanelFixture.Create(toolbarViewModel);

            toolbarViewModel.SelectToolCommand.Execute(tool);
            fixture.Window.UpdateLayout();

            Assert.Equal(tool, toolbarViewModel.ActiveTool);
            Assert.Equal(Visibility.Visible, fixture.VisibilityOf(tool));
            Assert.NotNull(fixture.HostContent(tool));
        });

    [Fact]
    public void HidingTheOwnerWindowThenRestoring_KeepsTheSameToolViewVisible()
        => WpfTestApplication.Run(() =>
        {
            // Production hides BOTH windows (WindowCoordinator.HideApplicationVisibility)
            // and the panel is owned by the toolbar, so an owner-driven hide is the
            // closest reproduction of the reported Ctrl+Alt+H round trip.
            var toolbarViewModel = new FloatingToolbarViewModel(ToolId.QuickChat);
            toolbarViewModel.SelectToolCommand.Execute(ToolId.QuickChat);
            using var fixture = PanelFixture.Create(toolbarViewModel);
            var owner = new Window
            {
                Left = -10_000,
                Top = -10_000,
                Width = 60,
                Height = 60,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                owner.Show();
                fixture.Window.Owner = owner;
                fixture.Window.UpdateLayout();
                Assert.Equal(Visibility.Visible, fixture.VisibilityOf(ToolId.QuickChat));

                // Hide panel, then owner — the production hide order.
                fixture.Window.Hide();
                owner.Hide();

                // Restore in the production order: owner (toolbar) first, then panel.
                owner.Show();
                fixture.Window.Show();
                fixture.Window.UpdateLayout();

                Assert.Equal(ToolId.QuickChat, toolbarViewModel.ActiveTool);
                Assert.Equal(PanelState.ActiveTool, toolbarViewModel.PanelState);
                Assert.Equal(Visibility.Visible, fixture.VisibilityOf(ToolId.QuickChat));
                Assert.Equal(
                    Visibility.Collapsed, fixture.VisibilityOf(ToolId.Translation));
            }
            finally
            {
                fixture.Window.Owner = null;
                owner.Close();
            }
        });

    [Fact]
    public void HideThenShow_KeepsAnInternalToolPageOpen()
        => WpfTestApplication.Run(() =>
        {
            var toolbarViewModel = new FloatingToolbarViewModel(ToolId.Translation);
            toolbarViewModel.SelectToolCommand.Execute(ToolId.Translation);
            using var fixture = PanelFixture.Create(toolbarViewModel);

            // Translation -> Saved Words is an internal page owned by the
            // Translation view model, so hiding the window must not reset it.
            fixture.Translation.OpenSavedWordsCommand.Execute(null);
            fixture.Window.UpdateLayout();
            Assert.True(fixture.Translation.IsSavedWordsPage);

            fixture.Window.Hide();
            fixture.Window.UpdateLayout();
            fixture.Window.Show();
            fixture.Window.UpdateLayout();

            Assert.True(fixture.Translation.IsSavedWordsPage);
            Assert.Equal(ToolId.Translation, toolbarViewModel.ActiveTool);
            Assert.Equal(Visibility.Visible, fixture.VisibilityOf(ToolId.Translation));
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProductionTaskbarCycles_PreservePanelPageAndInstances(bool panelVisible)
        => WpfTestApplication.Run(() =>
        {
            var vm = new FloatingToolbarViewModel(ToolId.Translation);
            vm.SelectToolCommand.Execute(ToolId.Translation);
            using var fixture = PanelFixture.Create(vm);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings();
            var store = new SettingsService(path);
            var placement = new WindowPlacementService();
            var toolbar = new ToolbarWindow(placement, store, settings);
            using var hotkeys = new GlobalHotkeyService();
            using var theme = new ThemeService(AppAppearanceMode.Dark, new AppAppearanceResolver(), new WindowsThemeWatcher(), new ResourceDictionary());
            var coordinator = fixture.Coordinator(toolbar, vm, placement, store, settings, hotkeys, theme);
            var originalMain = Application.Current.MainWindow;
            try
            {
                coordinator.ShowToolbar();
                coordinator.ShowPanel();
                fixture.Translation.OpenSavedWordsCommand.Execute(null);
                var content = fixture.HostContent(ToolId.Translation);
                if (!panelVisible) coordinator.HidePanel();
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    typeof(WindowCoordinator).GetMethod("ToggleApplicationVisibility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(coordinator, null);
                    DrainDispatcher();
                    Assert.Equal(WindowState.Minimized, toolbar.WindowState);
                    Assert.True(toolbar.IsVisible);
                    Assert.False(toolbar.CanUseNormalPlacement);
                    if (cycle % 2 == 0) toolbar.RestoreUi(); // Native taskbar restoration also changes WindowState.
                    else typeof(WindowCoordinator).GetMethod("ToggleApplicationVisibility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(coordinator, null);
                    DrainDispatcher();
                    Assert.Equal(WindowState.Normal, toolbar.WindowState);
                    Assert.True(toolbar.CanUseNormalPlacement);
                    Assert.Equal(panelVisible, fixture.Window.IsVisible);
                    Assert.Equal(ToolId.Translation, vm.ActiveTool);
                    Assert.Equal(PanelState.ActiveTool, vm.PanelState);
                    Assert.True(fixture.Translation.IsSavedWordsPage);
                    Assert.Same(content, fixture.HostContent(ToolId.Translation));
                    Assert.True(toolbar.Topmost);
                    Assert.True(fixture.Window.Topmost);
                    Assert.True(toolbar.ShowInTaskbar);
                    Assert.False(fixture.Window.ShowInTaskbar);
                    Assert.Same(toolbar, fixture.Window.Owner);
                }
                Assert.Same(originalMain, Application.Current.MainWindow);
                var saved = settings.WindowPlacement;
                toolbar.MinimizeUi();
                toolbar.CompletePendingDrag();
                coordinator.CloseAll();
                Assert.Equal(saved, store.Load().WindowPlacement);
            }
            finally { coordinator.CloseAll(); System.IO.File.Delete(path); }
        });

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [Theory]
    [InlineData("Hello from OCR", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   \t", false)]
    public void OcrShortcutWithClosedPanel_OnlyUsefulTextOpensTranslation(
        string? capturedText,
        bool shouldOpenTranslation)
        => WpfTestApplication.Run(() =>
        {
            using var harness = ShortcutHarness.Create(ToolId.Calendar, capturedText);
            harness.ClosePanel();

            harness.RunCapture();

            Assert.Equal(shouldOpenTranslation, harness.Panel.IsVisible);
            Assert.Equal(
                shouldOpenTranslation ? ToolId.Translation : ToolId.Calendar,
                harness.ViewModel.ActiveTool);
            Assert.Equal(
                shouldOpenTranslation ? "Hello from OCR" : string.Empty,
                harness.Translation.InputText);
        });

    [Fact]
    public void OcrShortcutWithVisibleOtherTool_KeepsPanelOpenAndShowsCapturedText()
        => WpfTestApplication.Run(() =>
        {
            using var harness = ShortcutHarness.Create(ToolId.QuickChat, "Visible capture");

            harness.RunCapture();

            Assert.True(harness.Panel.IsVisible);
            Assert.Equal(ToolId.Translation, harness.ViewModel.ActiveTool);
            Assert.Equal("Visible capture", harness.Translation.InputText);
        });

    [Fact]
    public void OcrShortcutWhileApplicationHidden_PreservesTranslationForNextRestore()
        => WpfTestApplication.Run(() =>
        {
            using var harness = ShortcutHarness.Create(ToolId.Calendar, "Hidden capture");
            harness.ToggleApplicationVisibility();
            Assert.Equal(WindowState.Minimized, harness.Toolbar.WindowState);
            Assert.False(harness.Panel.IsVisible);

            harness.RunCapture();

            Assert.Equal(WindowState.Minimized, harness.Toolbar.WindowState);
            Assert.False(harness.Panel.IsVisible);
            Assert.Equal(ToolId.Translation, harness.ViewModel.ActiveTool);
            Assert.Equal("Hidden capture", harness.Translation.InputText);

            harness.ToggleApplicationVisibility();

            Assert.Equal(WindowState.Normal, harness.Toolbar.WindowState);
            Assert.True(harness.Panel.IsVisible);
            Assert.Equal(ToolId.Translation, harness.ViewModel.ActiveTool);
            Assert.Equal("Hidden capture", harness.Translation.InputText);
        });

    [Theory]
    [InlineData(false, true, "success")]
    [InlineData(false, false, "success")]
    [InlineData(true, true, "success")]
    [InlineData(false, true, "cancel")]
    [InlineData(true, true, "cancel")]
    [InlineData(false, false, "failure")]
    [InlineData(false, true, "restore")]
    [InlineData(true, true, "restore")]
    [InlineData(false, true, "query-restore")]
    [InlineData(true, true, "hotkey-restore")]
    [InlineData(false, true, "hide-during-capture")]
    [InlineData(false, true, "shutdown")]
    [InlineData(false, true, "real-overlay")]
    public void ProductionCapture_PreservesTaskbarAndRestoresOnlyAfterCleanup(bool initiallyHidden, bool panelVisible, string outcome)
        => WpfTestApplication.Run(() =>
        {
            var vm = new FloatingToolbarViewModel(ToolId.Translation);
            vm.SelectToolCommand.Execute(ToolId.Translation);
            using var fixture = PanelFixture.Create(vm);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings();
            var store = new SettingsService(path);
            var placement = new WindowPlacementService();
            var toolbar = new ToolbarWindow(placement, store, settings);
            var overlay = new ControlledOverlay();
            ScreenCaptureOverlayWindow? realOverlay = null;
            var capture = new ScreenTextCaptureService(placement, new FakeRegionCapture(), new FakeOcr(), monitor =>
            {
                if (outcome == "real-overlay") return realOverlay = new ScreenCaptureOverlayWindow(monitor);
                return overlay;
            }, () => placement.GetMonitors().First(monitor => monitor.IsPrimary));
            using var hotkeys = new GlobalHotkeyService();
            using var theme = new ThemeService(AppAppearanceMode.Dark, new AppAppearanceResolver(), new WindowsThemeWatcher(), new ResourceDictionary());
            var coordinator = fixture.Coordinator(toolbar, vm, placement, store, settings, hotkeys, theme, capture);
            var originalMain = Application.Current.MainWindow;
            var auxiliary = new Window { ShowInTaskbar = false, ShowActivated = false, Left = -10000 };
            var toolbarHiddenEvents = 0;
            try
            {
                Application.Current.MainWindow = toolbar;
                coordinator.ShowToolbar();
                coordinator.ShowPanel();
                if (!panelVisible) coordinator.HidePanel();
                if (initiallyHidden)
                    typeof(WindowCoordinator).GetMethod("ToggleApplicationVisibility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(coordinator, null);
                var normalPlacement = settings.WindowPlacement;
                var content = fixture.HostContent(ToolId.Translation);
                auxiliary.Show();
                toolbar.IsVisibleChanged += (_, _) => { if (!toolbar.IsVisible) toolbarHiddenEvents++; };
                using var cancel = new CancellationTokenSource();
                var task = capture.CaptureTextAsync(cancel.Token);
                DrainDispatcher();
                Assert.True(capture.IsCapturing);
                Assert.False(task.IsCompleted);
                Assert.Equal(WindowState.Minimized, toolbar.WindowState);
                Assert.True(toolbar.IsVisible);
                Assert.True(toolbar.ShowInTaskbar);
                Assert.False(auxiliary.IsVisible);
                Assert.Equal(0, toolbarHiddenEvents);
                if (realOverlay is not null)
                {
                    Assert.Same(toolbar, realOverlay.Owner);
                    Assert.True(realOverlay.IsVisible);
                    Assert.True(realOverlay.Topmost);
                }
                else Assert.Same(toolbar, overlay.Owner);

                switch (outcome)
                {
                    case "success": overlay.Selection.SetResult(new PixelRect(0, 0, 10, 10)); break;
                    case "failure": overlay.Selection.SetException(new InvalidOperationException("Selection failed")); break;
                    case "restore":
                    case "query-restore":
                        RequestNativeRestore(new WindowInteropHelper(toolbar).Handle, outcome == "restore" ? 0x0112 : 0x0013, new IntPtr(0xF120), IntPtr.Zero);
                        Assert.Equal(WindowState.Minimized, toolbar.WindowState);
                        Assert.False(overlay.Closed);
                        break;
                    case "hotkey-restore":
                    case "hide-during-capture":
                        typeof(WindowCoordinator).GetMethod("ToggleApplicationVisibility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(coordinator, null);
                        if (outcome == "hide-during-capture") cancel.Cancel();
                        break;
                    case "shutdown": coordinator.CloseAll(); break;
                    default: cancel.Cancel(); break;
                }
                WaitForCapture(task);
                DrainDispatcher();
                Assert.True(task.IsCompleted);
                var result = task.GetAwaiter().GetResult();
                Assert.Equal(outcome == "success" ? ScreenTextCaptureStatus.Success : outcome == "failure" ? ScreenTextCaptureStatus.Failed : ScreenTextCaptureStatus.Cancelled, result.Status);
                Assert.False(capture.IsCapturing);
                if (realOverlay is not null) Assert.False(realOverlay.IsVisible);
                else Assert.True(overlay.Closed);
                if (outcome == "shutdown")
                {
                    Assert.False(toolbar.IsVisible);
                    Assert.False(auxiliary.IsVisible);
                }
                else
                {
                    Assert.Equal(0, toolbarHiddenEvents);
                    Assert.True(auxiliary.IsVisible);
                    var remainsHidden = (initiallyHidden && outcome != "restore" && outcome != "hotkey-restore") || outcome == "hide-during-capture";
                    Assert.Equal(remainsHidden ? WindowState.Minimized : WindowState.Normal, toolbar.WindowState);
                    if (!remainsHidden) Assert.Equal(panelVisible, fixture.Window.IsVisible);
                    else Assert.Equal(normalPlacement, settings.WindowPlacement);
                    Assert.Same(content, fixture.HostContent(ToolId.Translation));
                    Assert.Equal(ToolId.Translation, vm.ActiveTool);
                }
            }
            finally
            {
                coordinator.CloseAll();
                auxiliary.Close();
                Application.Current.MainWindow = originalMain;
                System.IO.File.Delete(path);
            }
        });

    private static void WaitForCapture(Task task)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        var timeout = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
        timeout.Tick += (_, _) => frame.Continue = false;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)), TaskScheduler.Default);
        timeout.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timeout.Stop(); }
        Assert.True(task.IsCompleted, "Capture must finish after its selection completes or is cancelled.");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr RequestNativeRestore(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private sealed class ControlledOverlay : IScreenCaptureOverlay
    {
        public Window? Owner { get; set; }
        public bool Closed { get; private set; }
        public TaskCompletionSource<PixelRect?> Selection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<PixelRect?> SelectAsync() => Selection.Task;
        public void CloseOverlay() { Closed = true; Selection.TrySetResult(null); }
    }

    private sealed class FakeRegionCapture : IScreenRegionCaptureService
    {
        public Task<CapturedScreenImage> CaptureAsync(PixelRect region, CancellationToken cancellationToken = default)
            => Task.FromResult(new CapturedScreenImage([1]));
    }

    private sealed class FakeOcr(string text = "Hello world") : ILocalOcrService
    {
        public Task<string> RecognizeEnglishAsync(CapturedScreenImage image, CancellationToken cancellationToken = default)
            => Task.FromResult(text);
        public void Dispose() { }
    }

    private sealed class ShortcutHarness : IDisposable
    {
        private readonly string _settingsPath;
        private readonly Window? _originalMainWindow;
        private readonly PanelFixture _fixture;
        private readonly WindowCoordinator _coordinator;
        private readonly GlobalHotkeyService _hotkeys;
        private readonly ThemeService _theme;
        private readonly ControlledOverlay _overlay;
        private readonly ScreenTextCaptureService _capture;
        private readonly string? _capturedText;

        private ShortcutHarness(
            string settingsPath,
            Window? originalMainWindow,
            PanelFixture fixture,
            WindowCoordinator coordinator,
            GlobalHotkeyService hotkeys,
            ThemeService theme,
            ToolbarWindow toolbar,
            FloatingToolbarViewModel viewModel,
            ControlledOverlay overlay,
            ScreenTextCaptureService capture,
            string? capturedText)
        {
            _settingsPath = settingsPath;
            _originalMainWindow = originalMainWindow;
            _fixture = fixture;
            _coordinator = coordinator;
            _hotkeys = hotkeys;
            _theme = theme;
            Toolbar = toolbar;
            ViewModel = viewModel;
            _overlay = overlay;
            _capture = capture;
            _capturedText = capturedText;
        }

        public ToolbarWindow Toolbar { get; }
        public PanelWindow Panel => _fixture.Window;
        public FloatingToolbarViewModel ViewModel { get; }
        public TranslationToolViewModel Translation => _fixture.Translation;

        public static ShortcutHarness Create(ToolId initialTool, string? capturedText)
        {
            var viewModel = new FloatingToolbarViewModel(initialTool);
            viewModel.SelectToolCommand.Execute(initialTool);
            var settingsPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            var settings = new AppSettings();
            var store = new SettingsService(settingsPath);
            var placement = new WindowPlacementService();
            var toolbar = new ToolbarWindow(placement, store, settings);
            var overlay = new ControlledOverlay();
            var capture = new ScreenTextCaptureService(
                placement,
                new FakeRegionCapture(),
                new FakeOcr(capturedText ?? string.Empty),
                _ => overlay,
                () => placement.GetMonitors().First(monitor => monitor.IsPrimary));
            var fixture = PanelFixture.Create(viewModel, capture);
            var hotkeys = new GlobalHotkeyService();
            var theme = new ThemeService(
                AppAppearanceMode.Dark,
                new AppAppearanceResolver(),
                new WindowsThemeWatcher(),
                new ResourceDictionary());
            var coordinator = fixture.Coordinator(
                toolbar, viewModel, placement, store, settings, hotkeys, theme, capture);
            var originalMainWindow = Application.Current.MainWindow;
            Application.Current.MainWindow = toolbar;
            coordinator.ShowToolbar();
            coordinator.ShowPanel();
            DrainDispatcher();
            return new ShortcutHarness(
                settingsPath,
                originalMainWindow,
                fixture,
                coordinator,
                hotkeys,
                theme,
                toolbar,
                viewModel,
                overlay,
                capture,
                capturedText);
        }

        public void ClosePanel()
        {
            ViewModel.ClosePanelCommand.Execute(null);
            DrainDispatcher();
            Assert.False(Panel.IsVisible);
        }

        public void ToggleApplicationVisibility()
        {
            InvokeCoordinator("ToggleApplicationVisibility");
            DrainDispatcher();
        }

        public void RunCapture()
        {
            InvokeCoordinator("TriggerExtractTextFromScreen");
            DrainDispatcher();
            Assert.True(_capture.IsCapturing);
            _overlay.Selection.SetResult(
                _capturedText is null ? null : new PixelRect(0, 0, 10, 10));
            WaitUntil(() => !_capture.IsCapturing
                && Translation.LastCaptureStatus is not null);
            DrainDispatcher();
        }

        private void InvokeCoordinator(string methodName) =>
            typeof(WindowCoordinator).GetMethod(
                methodName,
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance)!.Invoke(_coordinator, null);

        private static void WaitUntil(Func<bool> condition)
        {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            var probe = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = TimeSpan.FromMilliseconds(10)
            };
            timeout.Tick += (_, _) => frame.Continue = false;
            probe.Tick += (_, _) =>
            {
                if (condition()) frame.Continue = false;
            };
            timeout.Start();
            probe.Start();
            try { Dispatcher.PushFrame(frame); }
            finally
            {
                probe.Stop();
                timeout.Stop();
            }
            Assert.True(condition(), "Shortcut capture did not reach its expected state.");
        }

        public void Dispose()
        {
            _coordinator.CloseAll();
            _fixture.Dispose();
            _hotkeys.Dispose();
            _theme.Dispose();
            Application.Current.MainWindow = _originalMainWindow;
            System.IO.File.Delete(_settingsPath);
        }
    }

    private sealed class PanelFixture(
        PanelWindow window,
        TranslationToolViewModel translation,
        NotesToolViewModel notes,
        QuickChatViewModel quickChat,
        CalendarToolViewModel calendar) : IDisposable
    {
        public PanelWindow Window { get; } = window;

        public TranslationToolViewModel Translation { get; } = translation;

        public WindowCoordinator Coordinator(ToolbarWindow toolbar, FloatingToolbarViewModel vm,
            WindowPlacementService placement, SettingsService store, AppSettings settings,
            GlobalHotkeyService hotkeys, ThemeService theme, ScreenTextCaptureService? capture = null) => new(toolbar, Window, vm,
                placement, store, settings, notes, new IdleQuickChatSession(), quickChat, calendar,
                Translation, hotkeys, theme, capture);

        public static PanelFixture Create(
            FloatingToolbarViewModel toolbarViewModel,
            IScreenTextCaptureService? screenTextCaptureService = null)
        {
            var savedWords = new SavedWordsService(new InMemorySavedWordsStore());
            savedWords.InitializeAsync().GetAwaiter().GetResult();
            var frequentWords = new FrequentWordsService(new InMemoryFrequentWordsStore());
            frequentWords.InitializeAsync().GetAwaiter().GetResult();
            var translation = new TranslationToolViewModel(
                new UnconfiguredTranslationService(),
                new InMemoryTranslationHistoryStore(),
                new NullClipboardService(),
                savedWords,
                new NullSavedWordsExportService(),
                frequentWords,
                new TestOpenAiConfigurationProvider(),
                screenTextCaptureService: screenTextCaptureService);
            var notes = new NotesToolViewModel(
                new EmptyNotesStore(),
                TimeSpan.Zero);
            notes.InitializeAsync().GetAwaiter().GetResult();
            var quickChat = new QuickChatViewModel(
                new IdleQuickChatSession(),
                new UnusedQuickChatImageStore());
            var calendar = new CalendarToolViewModel(
                new CalendarSettings { Language = CalendarLanguageMode.English },
                new CalendarLanguageResolver(() => new CultureInfo("en-US")),
                new HebrewCalendarHolidayProvider(),
                () => new DateOnly(2026, 9, 17));
            var appSettings = new AppSettings();
            var settings = new SettingsViewModel(
                appSettings,
                new NoOpAppSettingsStore(),
                new EmptyApiKeyStore(),
                new TestOpenAiConfigurationProvider(),
                new UnusedConnectionTester(),
                _ => { });

            var window = new PanelWindow(
                toolbarViewModel,
                translation,
                notes,
                quickChat,
                calendar,
                settings)
            {
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            window.Show();
            window.UpdateLayout();
            return new PanelFixture(window, translation, notes, quickChat, calendar);
        }

        /// <summary>Materialized content of a tool host, or null if never opened.</summary>
        public object? HostContent(ToolId tool) =>
            ((ContentControl)Window.FindName(HostName(tool))!).Content;

        public object? SettingsContent() =>
            ((ContentControl)Window.FindName("ApplicationSettings")!).Content;

        private static string HostName(ToolId tool) => tool switch
        {
            ToolId.Translation => "TranslationTool",
            ToolId.Notes => "NotesTool",
            ToolId.QuickChat => "QuickChatTool",
            ToolId.Calendar => "CalendarTool",
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
        };

        public Visibility VisibilityOf(ToolId tool)
        {
            var name = tool switch
            {
                ToolId.Translation => "TranslationTool",
                ToolId.Notes => "NotesTool",
                ToolId.QuickChat => "QuickChatTool",
                ToolId.Calendar => "CalendarTool",
                _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
            };
            var element = (FrameworkElement)Window.FindName(name)!;
            return element.Visibility;
        }

        public void Dispose()
        {
            if (Window.IsLoaded)
            {
                Window.Close();
            }
        }
    }

    private sealed class EmptyNotesStore : INotesStore
    {
        public Task<NotesStorageState> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new NotesStorageState { Notes = [] });

        public Task SaveAsync(
            NotesStorageState state,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class IdleQuickChatSession : IActiveQuickChatConversation
    {
        public event EventHandler<QuickChatConversationChangedEventArgs>? Changed;

        public QuickChatConversationState CurrentState { get; } = new();

        public IReadOnlyList<QuickChatMessage> Messages => CurrentState.Messages;

        public string? AdditionalInstructions => null;

        public bool IsGenerating => false;

        public bool IsInitialized => true;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.Reset));
            return Task.CompletedTask;
        }

        public Task SendAsync(
            string? text,
            IReadOnlyList<QuickChatAttachment>? attachments = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RetryAsync(
            Guid assistantMessageId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NewChatAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAdditionalInstructionsAsync(
            string? instructions,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteUnsentAttachmentAsync(
            string assetFileName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PrepareForExitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class UnusedQuickChatImageStore : IQuickChatImageStore
    {
        public Task<ManagedQuickChatImage> ImportFileAsync(
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ManagedQuickChatImage> ImportBytesAsync(
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string GetAbsolutePath(string assetFileName) => assetFileName;

        public IReadOnlyList<string> GetManagedAssetFileNames() => [];

        public Task DeleteAsync(
            string assetFileName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Load() => new();

        public bool Save(AppSettings settings) => true;
    }

    private sealed class EmptyApiKeyStore : ISecureApiKeyStore
    {
        public bool HasKey => false;

        public string? Load() => null;

        public void Save(string apiKey)
        {
        }

        public void Remove()
        {
        }
    }

    private sealed class UnusedConnectionTester : IOpenAiConnectionTester
    {
        public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionTestResult(false, "not configured"));
    }
}
