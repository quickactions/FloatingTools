using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class NotesToolbarDragCaretRuntimeTests
{
    [Fact]
    public void DragRestore_PreservesMiddleCaretAndTypingAcrossRepeatedDrags() =>
        RunWithNote("Alpha beta gamma", (view, window, editor) =>
        {
            editor.Focus();
            editor.CaretIndex = 6;
            for (var drag = 0; drag < 2; drag++)
            {
                window.Hide();
                view.RestoreAfterToolbarDrag(window.Show);
                Drain();
                Assert.Equal(6, editor.CaretIndex);
            }

            editor.SelectedText = "X";
            Assert.Equal("Alpha Xbeta gamma", editor.Text);
        });

    [Fact]
    public void DragRestore_PreservesSelection() =>
        RunWithNote("Alpha beta gamma", (view, window, editor) =>
        {
            editor.Focus();
            editor.Select(6, 4);
            window.Hide();
            view.RestoreAfterToolbarDrag(window.Show);
            Drain();

            Assert.Equal(6, editor.SelectionStart);
            Assert.Equal(4, editor.SelectionLength);
            Assert.Equal("beta", editor.SelectedText);
        });

    [Fact]
    public void OrdinaryReentry_StillFocusesEnd() =>
        RunWithNote("Alpha beta gamma", (_, window, editor) =>
        {
            editor.Focus();
            editor.CaretIndex = 6;
            window.Hide();
            window.Show();
            Drain();
            Assert.Equal(editor.Text.Length, editor.CaretIndex);
        });

    [Fact]
    public void NewView_StillFocusesEnd() =>
        RunWithNote("Alpha beta gamma", (_, _, editor) =>
            Assert.Equal(editor.Text.Length, editor.CaretIndex));
    private static void RunWithNote(
        string text,
        Action<NotesToolView, Window, NoteDirectionalTextBox> assertion)
    {
        WpfTestApplication.Run(() =>
        {
            var view = new NotesToolView
            {
                DataContext = CreateViewModel(text)

            };
            var window = new Window
            {
                Content = view,
                Width = 340,
                Height = 320,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -10000,
                Top = -10000
            };
            try
            {
                window.Show();
                Drain();
                var editor = Descendants(view).OfType<NoteDirectionalTextBox>()
                    .Single(element => element.Name == "TextBlockEditor");
                assertion(view, window, editor);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Drain() =>
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static NotesToolViewModel CreateViewModel(string text)
    {
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(),
            Title = "Caret test",
            Blocks = new ObservableCollection<NoteBlock>(
                [new TextNoteBlock { Text = text }]),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var viewModel = new NotesToolViewModel(new RecordingStore(
            new NotesStorageState
            {
                Notes = [note],
                LastOpenedNoteId = note.Id
            }), TimeSpan.Zero);
        viewModel.InitializeAsync().GetAwaiter().GetResult();
        return viewModel;
    }

    private sealed class RecordingStore(NotesStorageState state) : INotesStore
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