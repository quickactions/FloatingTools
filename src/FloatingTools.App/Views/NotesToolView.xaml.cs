using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Popups;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Views;

public partial class NotesToolView : UserControl
{
    private ImageResizeSession? _imageResizeSession;

    public FrameworkElement NotesContentSurfaceElement => NotesContentSurface;

    public AnchoredPopupHost NotesPopupHostElement => NotesPopupHost;

    public NotesToolView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public void FocusSearch()
    {
        if (DataContext is NotesToolViewModel viewModel)
        {
            viewModel.IsMenuOpen = true;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                NotesSearchTextBox.Focus();
                NotesSearchTextBox.SelectAll();
            });
    }

    private async void NotesToolView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not NotesToolViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Z
            && Keyboard.Modifiers == ModifierKeys.Control
            && viewModel.CanUndoDocumentOperation)
        {
            if (Keyboard.FocusedElement is TextBox { CanUndo: true })
            {
                return;
            }

            await viewModel.UndoDocumentOperationAsync();
            FocusTextBlock(viewModel.ActiveBlocks.OfType<TextNoteBlock>().LastOrDefault());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C
            && Keyboard.Modifiers == ModifierKeys.Control
            && viewModel.SelectedImageBlock is not null)
        {
            CopySelectedImage(viewModel);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.X
            && Keyboard.Modifiers == ModifierKeys.Control
            && viewModel.SelectedImageBlock is not null)
        {
            if (CopySelectedImage(viewModel))
            {
                await viewModel.DeleteSelectedImageAsync();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back
            && Keyboard.Modifiers == ModifierKeys.None
            && Keyboard.FocusedElement is TextBox { Text.Length: 0, CaretIndex: 0 } editor
            && editor.DataContext is TextNoteBlock emptyText)
        {
            var previous = await viewModel.MergeEmptyTextBlockBackwardAsync(emptyText);
            if (!viewModel.ActiveBlocks.Contains(emptyText))
            {
                FocusTextBlock(previous);
                e.Handled = true;
                return;
            }
        }

        if (e.Key is Key.Delete or Key.Back
            && Keyboard.Modifiers == ModifierKeys.None
            && viewModel.SelectedImageBlock is not null
            && (e.Key != Key.Back
                || Keyboard.FocusedElement is not TextBoxBase))
        {
            await viewModel.DeleteSelectedImageAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.V
            && Keyboard.Modifiers == ModifierKeys.Control
            && Clipboard.ContainsImage())
        {
            var pngBytes = GetClipboardPngBytes();
            if (pngBytes is not null
                && FindDataContext<TextNoteBlock>(e.OriginalSource as DependencyObject) is { } textBlock
                && FindAncestor<TextBox>(e.OriginalSource as DependencyObject) is { } textBox)
            {
                var next = await viewModel.InsertClipboardImageAsync(
                    textBlock,
                    textBox.SelectionStart,
                    textBox.SelectionLength,
                    pngBytes,
                    GetAvailableImageWidth());
                FocusTextBlock(next);
                e.Handled = true;
            }
            else if (pngBytes is not null && viewModel.SelectedImageBlock is { } selectedImage)
            {
                var next = await viewModel.InsertClipboardImageAfterBlockAsync(
                    selectedImage,
                    pngBytes,
                    GetAvailableImageWidth());
                FocusTextBlock(next);
                e.Handled = true;
            }

            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control
            && Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            viewModel.NewNoteCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.N
                 && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            viewModel.NewTemporaryNoteCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TextBlockEditor_OnGotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel)
        {
            viewModel.SelectImage(null);
        }

    }

    private async void TextBlockEditor_OnLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel)
        {
            await viewModel.NormalizeActiveTextBlocksAsync();
        }
    }

    private void TextBlockEditor_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ClickCount != 3 || sender is not TextBox textBox)
        {
            return;
        }

        var characterIndex = textBox.GetCharacterIndexFromPoint(e.GetPosition(textBox), true);
        var selection = NotesTextSelection.GetLogicalLine(
            textBox.Text,
            characterIndex < 0 ? textBox.CaretIndex : characterIndex);
        textBox.Focus();
        textBox.Select(selection.Start, selection.Length);
        e.Handled = true;
    }

    private void NotePage_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (FindAncestor<TextBoxBase>(source) is not null
            || FindAncestor<Image>(source) is not null
            || FindAncestor<Thumb>(source) is not null
            || FindAncestor<ScrollBar>(source) is not null
            || FindDataContext<ImageNoteBlock>(source) is not null
            || FindDataContext<LinkListNoteBlock>(source) is not null
            || DataContext is not NotesToolViewModel viewModel)
        {
            return;
        }

        if (FindInsertionZone(source) is not null)
        {
            return;
        }

        var editors = FindDescendants<FrameworkElement>(NoteBlocksItemsControl)
            .OfType<TextBox>()
            .Where(editor => editor.DataContext is TextNoteBlock)
            .Select(editor => new
            {
                Editor = editor,
                Top = editor.TranslatePoint(new Point(0, 0), NoteBlocksItemsControl).Y,
                Bottom = editor.TranslatePoint(
                    new Point(0, editor.ActualHeight),
                    NoteBlocksItemsControl).Y
            })
            .OrderBy(item => item.Top)
            .ToArray();

        if (editors.Length == 0) return;

        var clickY = e.GetPosition(NoteBlocksItemsControl).Y;
        var target = NotesPageFocusResolver.Resolve(
            clickY,
            editors.Select(item => new EditableTextBounds(
                item.Top,
                item.Bottom,
                item.Editor.Text.Length)).ToArray())!.Value;
        FocusTextEditor(
            editors[target.BlockIndex].Editor,
            target.CaretIndex);
        e.Handled = true;
    }

    private void ImageBlock_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel
            && FindDataContext<ImageNoteBlock>(e.OriginalSource as DependencyObject) is { } image)
        {
            viewModel.SelectImage(image);
            if (e.ClickCount == 2)
            {
                OpenImageViewer(image);
            }

            Focus();
            e.Handled = true;
        }
    }

    private void OpenImageViewer(ImageNoteBlock image)
    {
        if (string.IsNullOrWhiteSpace(image.AssetPath) || !File.Exists(image.AssetPath))
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var placementService = new WindowPlacementService();
        var monitors = placementService.GetMonitors();
        var handle = owner is null ? IntPtr.Zero : new System.Windows.Interop.WindowInteropHelper(owner).Handle;
        var monitor = handle != IntPtr.Zero
            ? WindowPlacementCalculator.SelectMonitor(
                placementService.GetWindowBounds(handle),
                monitors)
            : monitors.First(monitor => monitor.IsPrimary);
        var viewer = new ImageViewerWindow(image.AssetPath, monitor);
        if (owner is not null)
        {
            viewer.Owner = owner;
        }

        viewer.ShowDialog();
    }

    private void TextBlockCopyMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetContextTextBlock(sender) is { } block
            && DataContext is NotesToolViewModel viewModel)
        {
            viewModel.CopyTextBlock(block);
        }
    }

    private async void TextBlockCutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetContextTextBlock(sender) is { } block
            && DataContext is NotesToolViewModel viewModel)
        {
            FocusTextBlock(await viewModel.CutTextBlockAsync(block));
        }
    }

    private async void TextBlockDeleteMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetContextTextBlock(sender) is { } block
            && DataContext is NotesToolViewModel viewModel)
        {
            FocusTextBlock(await viewModel.DeleteTextBlockAsync(block));
        }
    }

    private async void InsertionAddTextMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetInsertionZone(sender) is not { } zone
            || DataContext is not NotesToolViewModel viewModel)
        {
            return;
        }

        FocusTextBlock(await viewModel.InsertTextBlockBeforeAsync(zone.Tag as NoteBlock));
    }

    private async void InsertionAddLinkListMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetInsertionZone(sender) is not { } zone || DataContext is not NotesToolViewModel viewModel) return;
        var block = await viewModel.InsertLinkListBlockBeforeAsync(zone.Tag as NoteBlock);
        if (block is not null) FocusLinkListBlock(block);
    }

    private void TrailingInsertionZone_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        InsertionZone_OnMouseLeftButtonDown(sender, e);
    }

    private void InsertionZone_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not Border zone
            || DataContext is not NotesToolViewModel viewModel)
        {
            return;
        }

        var insertionIndex = ReferenceEquals(zone, TrailingInsertionZone)
            ? viewModel.ActiveBlocks.Count
            : zone.Tag is NoteBlock anchor
                ? viewModel.ActiveBlocks.IndexOf(anchor)
                : -1;
        if (insertionIndex < 0)
        {
            return;
        }

        switch (InsertionZoneFocusResolver.Resolve(viewModel.ActiveBlocks, insertionIndex))
        {
            case TextNoteBlock textBlock:
                FocusTextBlock(textBlock);
                break;
            case LinkListNoteBlock linkListBlock:
                FocusLinkListBlock(linkListBlock);
                break;
        }

        e.Handled = true;
    }

    private void ImageBlock_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel
            && FindDataContext<ImageNoteBlock>(e.OriginalSource as DependencyObject) is { } image)
        {
            viewModel.SelectImage(image);
        }
    }

    private void ImageResizeThumb_OnDragStarted(
        object sender,
        DragStartedEventArgs e)
    {
        if (sender is not Thumb { DataContext: ImageNoteBlock image })
        {
            return;
        }

        if (_imageResizeSession is not null)
        {
            return;
        }

        var stableMaximumWidth = GetAvailableImageWidth();
        _imageResizeSession = new ImageResizeSession(
            image,
            new NotesImageResizeState(
                image.DisplayWidth,
                NotesToolViewModel.MinimumImageWidth,
                stableMaximumWidth),
            Mouse.GetPosition(NoteBlocksItemsControl));
    }

    private void ImageResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_imageResizeSession is not { } session)
        {
            return;
        }

        var pointer = Mouse.GetPosition(NoteBlocksItemsControl);
        var horizontalChange = pointer.X - session.StartPointer.X;
        var verticalChange = pointer.Y - session.StartPointer.Y;
        session.State.TryUpdate(
            horizontalChange,
            verticalChange,
            session.Image.AspectRatio,
            proposedWidth => session.Image.SetResizePreview(proposedWidth));
    }

    private async void ImageResizeThumb_OnDragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        if (_imageResizeSession is not { } session)
        {
            return;
        }

        _imageResizeSession = null;
        if (sender is Thumb thumb && thumb.IsMouseCaptured)
        {
            thumb.ReleaseMouseCapture();
        }

        try
        {
            if (!e.Canceled
                && DataContext is NotesToolViewModel viewModel
                && session.State.TryComplete(out var finalWidth))
            {
                await viewModel.CommitImageResizeAsync(
                    session.Image,
                    finalWidth,
                    session.State.MaximumWidth);
            }
        }
        finally
        {
            session.Image.SetResizePreview(null);
        }
    }

    private void ImageCopyMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel
            && GetContextImage(sender) is { } image)
        {
            viewModel.SelectImage(image);
            CopySelectedImage(viewModel);
        }
    }

    private async void ImageCutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel
            && GetContextImage(sender) is { } image)
        {
            viewModel.SelectImage(image);
            if (CopySelectedImage(viewModel))
            {
                await viewModel.DeleteSelectedImageAsync();
            }
        }
    }

    private async void ImageDeleteMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel
            && GetContextImage(sender) is { } image)
        {
            viewModel.SelectImage(image);
            await viewModel.DeleteSelectedImageAsync();
        }
    }

    private void NotesToolView_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetSupportedDroppedImage(e.Data) is null
            ? DragDropEffects.None
            : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void NotesToolView_OnPreviewDrop(object sender, DragEventArgs e)
    {
        var sourcePath = GetSupportedDroppedImage(e.Data);
        if (sourcePath is null
            || DataContext is not NotesToolViewModel viewModel
            || viewModel.ActiveBlocks.Count == 0)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        var target = FindDataContext<NoteBlock>(source)
            ?? viewModel.ActiveBlocks[^1];
        int? textPosition = null;
        if (target is TextNoteBlock
            && FindAncestor<TextBox>(source) is { } textBox)
        {
            var point = e.GetPosition(textBox);
            var characterIndex = textBox.GetCharacterIndexFromPoint(point, true);
            textPosition = characterIndex < 0 ? textBox.Text.Length : characterIndex;
        }

        try
        {
            var next = await viewModel.InsertImageFileAsync(
                target,
                textPosition,
                sourcePath,
                GetAvailableImageWidth());
            FocusTextBlock(next);
            e.Effects = DragDropEffects.Copy;
        }
        catch (IOException)
        {
            e.Effects = DragDropEffects.None;
        }
        catch (NotSupportedException)
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void RenameMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetContextNote(sender) is { } note
            && DataContext is NotesToolViewModel viewModel)
        {
            viewModel.BeginRenameCommand.Execute(note);
        }
    }

    private void DeleteMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetContextNote(sender) is { } note
            && DataContext is NotesToolViewModel viewModel)
        {
            viewModel.DeleteNoteCommand.Execute(note);
        }
    }

    private void ExportPdfMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetContextNote(sender) is { } note
            && DataContext is NotesToolViewModel viewModel)
        {
            viewModel.ExportNotePdfCommand.Execute(note);
        }
    }

    private void ExportWordMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetContextNote(sender) is { } note
            && DataContext is NotesToolViewModel viewModel)
        {
            viewModel.ExportNoteWordCommand.Execute(note);
        }
    }

    private void NoteActionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void RenameTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not NotesToolViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.SaveRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RenameTextBox_OnLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is NotesToolViewModel viewModel
            && viewModel.RenamingNote is not null)
        {
            viewModel.CancelRenameCommand.Execute(null);
        }
    }

    private void RenameTextBox_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || sender is not TextBox textBox)
        {
            return;
        }

        textBox.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                textBox.Focus();
                textBox.SelectAll();
            });
    }

    private static NoteDocument? GetContextNote(object sender)
        => NoteMenuContextResolver.Resolve(sender as MenuItem);

    private static ImageNoteBlock? GetContextImage(object sender)
    {
        if (sender is not MenuItem menuItem)
        {
            return null;
        }

        return menuItem.DataContext as ImageNoteBlock
            ?? ((menuItem.Parent as ContextMenu)?.PlacementTarget as FrameworkElement)
                ?.DataContext as ImageNoteBlock;
    }

    private static TextBox? GetContextTextBox(object sender) =>
        GetContextMenu(sender)?.PlacementTarget as TextBox;

    private static TextNoteBlock? GetContextTextBlock(object sender) =>
        GetContextTextBox(sender)?.DataContext as TextNoteBlock;

    private static Border? GetInsertionZone(object sender) =>
        GetContextMenu(sender)?.PlacementTarget as Border;

    private static Border? FindInsertionZone(DependencyObject? source)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is Border { Name: "InsertionZone" or "TrailingInsertionZone" } zone)
            {
                return zone;
            }
        }

        return null;
    }

    private static ContextMenu? GetContextMenu(object sender) =>
        sender is MenuItem menuItem
            ? ItemsControl.ItemsControlFromItemContainer(menuItem) as ContextMenu
                ?? menuItem.Parent as ContextMenu
            : null;

    private static bool CopySelectedImage(NotesToolViewModel viewModel)
    {
        var path = viewModel.SelectedImageBlock?.AssetPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var image = decoder.Frames[0];
            image.Freeze();
            Clipboard.SetImage(image);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or NotSupportedException
            or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static byte[]? GetClipboardPngBytes()
    {
        var image = Clipboard.GetImage();
        if (image is null)
        {
            return null;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            FocusTextBlock((DataContext as NotesToolViewModel)?.ActiveBlocks
                .OfType<TextNoteBlock>()
                .LastOrDefault());
        }
    }

    private double GetAvailableImageWidth() =>
        Math.Max(
            NotesToolViewModel.MinimumImageWidth,
            NoteBlocksItemsControl.ActualWidth - 32);

    private void FocusTextBlock(TextNoteBlock? block)
    {
        if (block is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                var editor = FindDescendants<FrameworkElement>(NoteBlocksItemsControl)
                    .OfType<TextBox>()
                    .FirstOrDefault(item => Equals(item.Tag, block.Id));
                if (editor is null)
                {
                    return;
                }

                FocusTextEditor(editor, editor.Text.Length);
            });
    }

    private void FocusLinkListBlock(LinkListNoteBlock block)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var control = FindDescendants<FrameworkElement>(NoteBlocksItemsControl)
                .OfType<LinkListBlockControl>()
                .FirstOrDefault(item => ReferenceEquals(item.DataContext, block));
            control?.FocusDraft();
        });
    }

    private static void FocusTextEditor(TextBox editor, int caretIndex)
    {
        editor.Focus();
        editor.CaretIndex = Math.Clamp(caretIndex, 0, editor.Text.Length);
    }

    private static string? GetSupportedDroppedImage(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop)
            || data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return null;
        }

        return paths.FirstOrDefault(path => IsSupportedImageExtension(
            Path.GetExtension(path)));
    }

    private static bool IsSupportedImageExtension(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);

    private static T? FindDataContext<T>(DependencyObject? source)
        where T : class
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is FrameworkElement { DataContext: T value })
            {
                return value;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is T value)
            {
                return value;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current) =>
        current is FrameworkContentElement contentElement
            ? contentElement.Parent
            : VisualTreeHelper.GetParent(current);

    private static IEnumerable<DependencyObject> FindDescendants<FrameworkElement>(
        DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in FindDescendants<FrameworkElement>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record ImageResizeSession(
        ImageNoteBlock Image,
        NotesImageResizeState State,
        Point StartPointer);
}
