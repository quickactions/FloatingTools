using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class NotesInsertionZoneFocusIntegrationTests
{
    [Fact]
    public void InsertionZoneClick_FocusesTheResolvedEditorWithoutChangingTheDocument()
        => RunSta(() =>
        {
            var text = new TextNoteBlock { Text = "Focus at the end" };
            var image = new ImageNoteBlock { AssetFileName = "missing-image.png" };
            var links = new LinkListNoteBlock();
            var viewModel = CreateViewModel(text, image, links);
            var view = new NotesToolView { DataContext = viewModel };
            var window = Show(view);

            try
            {
                var blocksBefore = viewModel.ActiveBlocks.ToArray();
                var editor = FindDescendant<TextBox>(
                    view, element => Equals(element.Tag, text.Id));
                var textZone = FindDescendant<Border>(
                    view, element => ReferenceEquals(element.Tag, image));
                Assert.NotNull(editor);
                Assert.NotNull(textZone);

                editor!.Focus();
                editor.Select(0, 1);
                RaiseLeftClick(textZone!);
                DrainDispatcher();

                Assert.True(editor.IsKeyboardFocusWithin);
                Assert.Equal(editor.Text.Length, editor.CaretIndex);
                Assert.Equal(0, editor.SelectionLength);
                Assert.Equal(blocksBefore, viewModel.ActiveBlocks);

                var trailingZone = FindDescendant<Border>(
                    view, element => element.Name == "TrailingInsertionZone");
                var linkControl = FindDescendant<LinkListBlockControl>(
                    view, element => ReferenceEquals(element.DataContext, links));
                var draft = FindDescendant<TextBox>(
                    linkControl!, element => element.Name == "DraftToken");
                Assert.NotNull(trailingZone);
                Assert.NotNull(linkControl);
                Assert.NotNull(draft);

                RaiseLeftClick(trailingZone!);
                DrainDispatcher();

                Assert.True(draft!.IsKeyboardFocusWithin);
                Assert.False(linkControl!.PopupAnchorService.IsOpen);
                Assert.Equal(blocksBefore, viewModel.ActiveBlocks);

                Assert.NotNull(textZone.ContextMenu);
                textZone.ContextMenu!.IsOpen = true;
                Assert.True(textZone.ContextMenu.IsOpen);
                textZone.ContextMenu.IsOpen = false;

                window.Close();

                var imageOnly = new ImageNoteBlock { AssetFileName = "missing-image.png" };
                var noTargetViewModel = CreateViewModel(imageOnly);
                var noTargetView = new NotesToolView { DataContext = noTargetViewModel };
                var noTargetWindow = Show(noTargetView);
                try
                {
                    var noTargetBlocksBefore = noTargetViewModel.ActiveBlocks.ToArray();
                    var noTargetZone = FindDescendant<Border>(
                        noTargetView, element => element.Name == "TrailingInsertionZone");
                    Assert.NotNull(noTargetZone);

                    RaiseLeftClick(noTargetZone!);
                    DrainDispatcher();

                    Assert.Equal(noTargetBlocksBefore, noTargetViewModel.ActiveBlocks);
                    Assert.Null(InsertionZoneFocusResolver.Resolve(
                        noTargetViewModel.ActiveBlocks,
                        noTargetViewModel.ActiveBlocks.Count));
                }
                finally
                {
                    noTargetWindow.Close();
                }
            }
            finally
            {
                if (window.IsVisible)
                {
                    window.Close();
                }
            }
        });

    private static NotesToolViewModel CreateViewModel(params NoteBlock[] blocks)
    {
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(),
            Title = "Focus test",
            Blocks = new ObservableCollection<NoteBlock>(blocks),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var viewModel = new NotesToolViewModel(new RecordingStore(new NotesStorageState
        {
            Notes = [note],
            LastOpenedNoteId = note.Id
        }), TimeSpan.Zero);
        viewModel.InitializeAsync().GetAwaiter().GetResult();
        return viewModel;
    }

    private static Window Show(NotesToolView view)
    {
        var window = new Window
        {
            Content = view,
            Width = 360,
            Height = 420,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void RaiseLeftClick(UIElement element) =>
        element.RaiseEvent(new MouseButtonEventArgs(
            Mouse.PrimaryDevice,
            Environment.TickCount,
            MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent
        });

    private static void DrainDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));

    private static T? FindDescendant<T>(
        DependencyObject root,
        Func<T, bool> predicate)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match && predicate(match))
            {
                return match;
            }

            if (FindDescendant(child, predicate) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static void RunSta(Action action) => WpfTestApplication.Run(action);

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
