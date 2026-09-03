using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class FocusSearchRuntimeTests
{
    [Fact]
    public void Translation_FocusSearch_NavigatesToSavedWordsAndSelectsExistingQuery()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = new TranslationToolViewModel(
                new UnconfiguredTranslationService(),
                new InMemoryTranslationHistoryStore(),
                new NullClipboardService(),
                new TestOpenAiConfigurationProvider());
            viewModel.SavedWords.SearchQuery = "hello";
            var view = new TranslationToolView { DataContext = viewModel };

            RunFocusSearch(view, view.FocusSearch, out var window);
            try
            {
                Assert.True(viewModel.IsSavedWordsPage);
                var textBox = FindByName<System.Windows.Controls.TextBox>(
                    view, "SavedWordsSearchTextBox");
                Assert.Same(textBox, Keyboard.FocusedElement);
                Assert.Equal("hello", textBox.SelectedText);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public void Notes_FocusSearch_OpensNotesMenuAndSelectsExistingQuery()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = new NotesToolViewModel(
                new RecordingNotesStore(new NotesStorageState { Notes = [] }),
                TimeSpan.Zero);
            viewModel.InitializeAsync().GetAwaiter().GetResult();
            viewModel.SearchQuery = "meeting";
            var view = new NotesToolView { DataContext = viewModel };

            RunFocusSearch(view, view.FocusSearch, out var window);
            try
            {
                Assert.True(viewModel.IsMenuOpen);
                var textBox = FindByName<System.Windows.Controls.TextBox>(
                    view, "NotesSearchTextBox");
                Assert.Same(textBox, Keyboard.FocusedElement);
                Assert.Equal("meeting", textBox.SelectedText);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public void Calendar_FocusSearch_OpensHeaderAndSelectsExistingQuery()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = new CalendarToolViewModel(
                new CalendarSettings { Language = CalendarLanguageMode.English },
                new CalendarLanguageResolver(() => new CultureInfo("en-US")),
                new HebrewCalendarHolidayProvider(),
                () => new DateOnly(2026, 9, 17));
            viewModel.SearchText = "birthday";
            var view = new CalendarToolView { DataContext = viewModel };

            RunFocusSearch(view, view.FocusSearch, out var window);
            try
            {
                Assert.True(viewModel.IsHeaderExpanded);
                var textBox = FindByName<System.Windows.Controls.TextBox>(
                    view, "CalendarSearchTextBox");
                Assert.Same(textBox, Keyboard.FocusedElement);
                Assert.Equal("birthday", textBox.SelectedText);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public void Calendar_FocusSearch_IsIdempotentAndDoesNotCollapseAnAlreadyOpenHeader()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = new CalendarToolViewModel(
                new CalendarSettings { Language = CalendarLanguageMode.English },
                new CalendarLanguageResolver(() => new CultureInfo("en-US")),
                new HebrewCalendarHolidayProvider(),
                () => new DateOnly(2026, 9, 17));
            var view = new CalendarToolView { DataContext = viewModel };

            RunFocusSearch(view, view.FocusSearch, out var window);
            try
            {
                Assert.True(viewModel.IsHeaderExpanded);

                view.FocusSearch();
                DrainDispatcher();

                Assert.True(viewModel.IsHeaderExpanded);
            }
            finally
            {
                window.Close();
            }
        });

    private static void RunFocusSearch(
        FrameworkElement view,
        Action focusSearch,
        out Window window)
    {
        window = new Window
        {
            Content = view,
            Width = 300,
            Height = 500,
            Left = -10_000,
            Top = -10_000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None
        };
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        focusSearch();
        DrainDispatcher();
        window.UpdateLayout();
    }

    private static void DrainDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));

    private static T FindByName<T>(FrameworkElement root, string name)
        where T : FrameworkElement =>
        (T?)root.FindName(name)
            ?? throw new InvalidOperationException($"Element '{name}' not found.");

    private sealed class RecordingNotesStore(NotesStorageState state) : INotesStore
    {
        public Task<NotesStorageState> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(state);

        public Task SaveAsync(
            NotesStorageState state,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
