using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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

    private sealed class PanelFixture(
        PanelWindow window,
        TranslationToolViewModel translation) : IDisposable
    {
        public PanelWindow Window { get; } = window;

        public TranslationToolViewModel Translation { get; } = translation;

        public static PanelFixture Create(FloatingToolbarViewModel toolbarViewModel)
        {
            var translation = new TranslationToolViewModel(
                new UnconfiguredTranslationService(),
                new InMemoryTranslationHistoryStore(),
                new NullClipboardService(),
                new TestOpenAiConfigurationProvider());
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
            return new PanelFixture(window, translation);
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
