using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.Notes;

/// <summary>
/// Coordinates active-note editing mechanics: subscriptions, undo snapshots,
/// debounce scheduling, and document-block mutations. Persistence policy remains
/// with the NotesToolViewModel facade.
/// </summary>
public sealed class ActiveNoteEditor
{
    private const double MinimumImageWidth = 60;
    private const double DefaultAvailableImageWidth = 560;
    private readonly TimeSpan _debounceDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly HashSet<NoteDocument> _subscribedNotes = [];
    private readonly Dictionary<NoteBlock, NoteDocument> _blockOwners = [];
    private readonly Dictionary<Guid, List<NoteUndoRecord>> _undoHistories = [];
    private int _changeTrackingSuppressionDepth;
    private CancellationTokenSource? _saveDebounce;
    private ImageNoteBlock? _selectedImageBlock;

    public ActiveNoteEditor(
        TimeSpan? debounceDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(850);
        _delayAsync = delayAsync ?? Task.Delay;
    }

    public event Action<NoteDocument>? NoteMutated;
    public event EventHandler<NotePropertyChangedEventArgs>? NotePropertyChanged;
    public event EventHandler? UndoHistoryChanged;
    public event EventHandler? SelectedImageChanged;

    public ImageNoteBlock? SelectedImageBlock => _selectedImageBlock;

    public Task ScheduleDebouncedSave(Func<CancellationToken, Task> saveAction)
    {
        CancelPendingSave();
        var cancellation = _saveDebounce = new CancellationTokenSource();
        return SaveAfterDelayAsync(cancellation, saveAction);
    }

    public void CancelPendingSave()
    {
        var cancellation = Interlocked.Exchange(ref _saveDebounce, null);
        cancellation?.Cancel();
    }

    private async Task SaveAfterDelayAsync(
        CancellationTokenSource cancellation,
        Func<CancellationToken, Task> saveAction)
    {
        try
        {
            await _delayAsync(_debounceDelay, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                await saveAction(cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_saveDebounce, cancellation)) _saveDebounce = null;
            cancellation.Dispose();
        }
    }

    public bool NormalizeTextBlocks(NoteDocument note) => NormalizeTextBlocksCore(note);

    public bool NormalizeTextBlocksCore(NoteDocument note)
    {
        var changed = false;
        for (var index = 1; index < note.Blocks.Count;)
        {
            if (note.Blocks[index - 1] is TextNoteBlock previous
                && note.Blocks[index] is TextNoteBlock current
                && !current.PreserveBoundaryBefore)
            {
                previous.Text += current.Text;
                note.Blocks.RemoveAt(index);
                changed = true;
                continue;
            }

            index++;
        }

        return changed;
    }

    public List<NoteBlock> CloneBlocks(IEnumerable<NoteBlock> blocks) =>
        blocks.Select(CloneBlock).ToList();

    public NoteBlock CloneBlock(NoteBlock block) => block switch
    {
        TextNoteBlock text => new TextNoteBlock
        {
            Id = text.Id,
            Text = text.Text,
            PreserveBoundaryBefore = text.PreserveBoundaryBefore
        },
        ImageNoteBlock image => new ImageNoteBlock
        {
            Id = image.Id,
            AssetFileName = image.AssetFileName,
            NaturalWidth = image.NaturalWidth,
            NaturalHeight = image.NaturalHeight,
            DisplayWidth = image.DisplayWidth,
            AssetPath = image.AssetPath
        },
        LinkListNoteBlock links => new LinkListNoteBlock
        {
            Id = links.Id,
            Items = new ObservableCollection<NoteLinkItem>(links.Items.Select(item => new NoteLinkItem
            {
                Id = item.Id,
                Url = item.Url,
                DisplayName = item.DisplayName
            }))
        },
        _ => throw new NotSupportedException(
            $"Unsupported note block type: {block.GetType().Name}")
    };

    public sealed record NoteUndoRecord(List<NoteBlock> Blocks);

    public bool HasUndoHistory(NoteDocument note) =>
        _undoHistories.TryGetValue(note.Id, out var history) && history.Count > 0;

    public TextBlockInsertionResult? InsertTextBlock(
        NoteDocument note,
        NoteBlock? anchor,
        int maximumUndoOperations)
    {
        var index = ResolveInsertionIndex(note, anchor);
        if (index < 0) return null;

        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        var block = new TextNoteBlock
        {
            PreserveBoundaryBefore = index > 0 && note.Blocks[index - 1] is TextNoteBlock
        };

        using (SuppressChangeTracking())
        {
            if (index < note.Blocks.Count && note.Blocks[index] is TextNoteBlock next)
            {
                next.PreserveBoundaryBefore = true;
            }

            note.Blocks.Insert(index, block);
        }

        return new TextBlockInsertionResult(block, evictedAssets);
    }

    public TextBlockMutationResult? DeleteTextBlock(
        NoteDocument note,
        TextNoteBlock block,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(block, out var owner) || !ReferenceEquals(owner, note)) return null;

        var index = note.Blocks.IndexOf(block);
        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        using (SuppressChangeTracking())
        {
            note.Blocks.RemoveAt(index);
            NormalizeTextBlocksCore(note);
        }

        var focusTarget = note.Blocks
            .Take(Math.Min(index + 1, note.Blocks.Count))
            .OfType<TextNoteBlock>()
            .LastOrDefault()
            ?? note.Blocks.OfType<TextNoteBlock>().FirstOrDefault();
        return new TextBlockMutationResult(focusTarget, evictedAssets);
    }

    public TextBlockMutationResult? MergeEmptyTextBlockBackward(
        NoteDocument note,
        TextNoteBlock emptyBlock,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(emptyBlock, out var owner)
            || !ReferenceEquals(owner, note)
            || !string.IsNullOrEmpty(emptyBlock.Text)) return null;

        var index = note.Blocks.IndexOf(emptyBlock);
        if (index < 0) return null;

        var previous = note.Blocks.Take(index).OfType<TextNoteBlock>().LastOrDefault();
        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        using (SuppressChangeTracking())
        {
            note.Blocks.RemoveAt(index);
            NormalizeTextBlocksCore(note);
        }

        return new TextBlockMutationResult(
            previous ?? note.Blocks.OfType<TextNoteBlock>().FirstOrDefault(),
            evictedAssets);
    }

    public ImageInsertionResult? InsertImageIntoText(
        NoteDocument note,
        TextNoteBlock target,
        int selectionStart,
        int selectionLength,
        ImageBlockData image,
        double availableWidth,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(target, out var owner) || !ReferenceEquals(owner, note)) return null;

        var start = Math.Clamp(selectionStart, 0, target.Text.Length);
        var length = Math.Clamp(selectionLength, 0, target.Text.Length - start);
        var before = target.Text[..start];
        var after = target.Text[(start + length)..];
        var imageBlock = CreateImageBlock(image, availableWidth);
        var afterBlock = new TextNoteBlock { Text = after };
        var index = note.Blocks.IndexOf(target);
        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);

        using (SuppressChangeTracking())
        {
            target.Text = before;
            note.Blocks.Insert(index + 1, imageBlock);
            note.Blocks.Insert(index + 2, afterBlock);
        }

        return new ImageInsertionResult(note, imageBlock, afterBlock, evictedAssets);
    }

    public ImageInsertionResult? InsertImageAfterBlock(
        NoteDocument note,
        NoteBlock target,
        ImageBlockData image,
        double availableWidth,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(target, out var owner) || !ReferenceEquals(owner, note)) return null;

        var imageBlock = CreateImageBlock(image, availableWidth);
        var afterBlock = new TextNoteBlock();
        var index = note.Blocks.IndexOf(target);
        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        using (SuppressChangeTracking())
        {
            note.Blocks.Insert(index + 1, imageBlock);
            note.Blocks.Insert(index + 2, afterBlock);
        }

        return new ImageInsertionResult(note, imageBlock, afterBlock, evictedAssets);
    }

    public ImageDeletionResult? DeleteImage(
        NoteDocument note,
        ImageNoteBlock image,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(image, out var owner) || !ReferenceEquals(owner, note)) return null;

        var assetFileName = image.AssetFileName;
        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        using (SuppressChangeTracking())
        {
            note.Blocks.Remove(image);
        }

        NormalizeActiveTextBlocks(note);
        SelectImage(null);
        return new ImageDeletionResult(assetFileName, evictedAssets);
    }

    public void ResizeImage(ImageNoteBlock image, double requestedWidth, double availableWidth) =>
        image.DisplayWidth = ClampImageWidth(requestedWidth, availableWidth);

    public ImageResizeCommitResult? CommitImageResize(
        NoteDocument note,
        ImageNoteBlock image,
        double requestedWidth,
        double availableWidth,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(image, out _)) return null;

        var finalWidth = ClampImageWidth(requestedWidth, availableWidth);
        if (Math.Abs(finalWidth - image.DisplayWidth) < 0.01) return null;

        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        image.DisplayWidth = finalWidth;
        return new ImageResizeCommitResult(evictedAssets);
    }

    public LinkListBlockInsertionResult? InsertLinkListBlock(
        NoteDocument note,
        NoteBlock? anchor,
        int maximumUndoOperations)
    {
        var index = ResolveInsertionIndex(note, anchor);
        if (index < 0) return null;

        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        var block = new LinkListNoteBlock();
        using (SuppressChangeTracking()) note.Blocks.Insert(index, block);
        return new LinkListBlockInsertionResult(note, block, evictedAssets);
    }

    public LinkTokenCommitMutationResult CommitLinkTokens(
        LinkListNoteBlock block,
        string input,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(block, out var note)) return new([], input);

        var accepted = new List<NoteLinkItem>();
        var invalid = new List<string>();
        var tokens = LinkTokenParser.Parse(input);
        if (tokens.Any(token => LinkUrlValidator.TryValidate(token, out _)))
        {
            PushUndoSnapshot(note, maximumUndoOperations);
        }

        foreach (var token in tokens)
        {
            if (!LinkUrlValidator.TryValidate(token, out var url))
            {
                invalid.Add(token);
                continue;
            }

            var item = new NoteLinkItem { Url = url, DisplayName = url };
            block.Items.Add(item);
            accepted.Add(item);
        }

        return new(accepted, string.Join(' ', invalid));
    }

    public LinkListMutationResult? DeleteLinkListBlock(
        LinkListNoteBlock block,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(block, out var note)) return null;
        return DeleteLinkListBlockCore(note, block, maximumUndoOperations);
    }

    public LinkListItemMutationResult? EditLinkItem(
        LinkListNoteBlock block,
        NoteLinkItem item,
        string displayName,
        string url,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(block, out var note)
            || !block.Items.Contains(item)
            || !LinkUrlValidator.TryValidate(url, out var safeUrl)) return null;

        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        item.Url = safeUrl;
        item.DisplayName = string.IsNullOrWhiteSpace(displayName) ? safeUrl : displayName.Trim();
        return new LinkListItemMutationResult(note, evictedAssets);
    }

    public LinkListItemMutationResult? DeleteLinkItem(
        LinkListNoteBlock block,
        NoteLinkItem item,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(block, out var note) || !block.Items.Contains(item)) return null;

        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        block.Items.Remove(item);
        return new LinkListItemMutationResult(note, evictedAssets);
    }

    public LinkListBackspaceMutationResult? DeleteLastLinkItemOrBlock(
        LinkListNoteBlock block,
        int maximumUndoOperations)
    {
        if (!TryGetOwner(block, out var note)) return null;

        if (block.Items.Count > 0)
        {
            var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
            block.Items.RemoveAt(block.Items.Count - 1);
            return new LinkListBackspaceMutationResult(note, false, evictedAssets);
        }

        var deleted = DeleteLinkListBlockCore(note, block, maximumUndoOperations);
        return new LinkListBackspaceMutationResult(deleted.Note, true, deleted.EvictedAssets);
    }

    public bool NormalizeActiveTextBlocks(NoteDocument note)
    {
        using (SuppressChangeTracking()) return NormalizeTextBlocks(note);
    }

    public string[] PushUndoSnapshot(NoteDocument note, int maximumUndoOperations)
    {
        if (!_undoHistories.TryGetValue(note.Id, out var history))
        {
            history = [];
            _undoHistories[note.Id] = history;
        }

        history.Add(new NoteUndoRecord(CloneBlocks(note.Blocks)));
        string[] evictedAssets = [];
        if (history.Count > maximumUndoOperations)
        {
            evictedAssets = history[0].Blocks
                .OfType<ImageNoteBlock>()
                .Select(image => image.AssetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
            history.RemoveAt(0);
        }

        UndoHistoryChanged?.Invoke(this, EventArgs.Empty);
        return evictedAssets;
    }

    public bool TryPopUndoSnapshot(NoteDocument note, out NoteUndoRecord record)
    {
        record = null!;
        if (!_undoHistories.TryGetValue(note.Id, out var history) || history.Count == 0)
        {
            return false;
        }

        record = history[^1];
        history.RemoveAt(history.Count - 1);
        if (history.Count == 0) _undoHistories.Remove(note.Id);
        UndoHistoryChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public string[] DiscardUndoHistory(Guid noteId)
    {
        if (!_undoHistories.Remove(noteId, out var history)) return [];

        UndoHistoryChanged?.Invoke(this, EventArgs.Empty);
        return GetImageAssetNames(history.SelectMany(record => record.Blocks));
    }

    public string[] DiscardAllUndoHistory()
    {
        var assets = GetImageAssetNames(_undoHistories.Values
            .SelectMany(history => history)
            .SelectMany(record => record.Blocks));
        _undoHistories.Clear();
        UndoHistoryChanged?.Invoke(this, EventArgs.Empty);
        return assets;
    }

    public IEnumerable<string> GetReferencedUndoAssetNames() =>
        _undoHistories.Values
            .SelectMany(history => history)
            .SelectMany(record => record.Blocks)
            .OfType<ImageNoteBlock>()
            .Select(image => image.AssetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name));

    public IEnumerable<string> GetImageAssetNames(NoteDocument note) =>
        GetImageAssetNames(note.Blocks);

    public void SelectImage(ImageNoteBlock? image)
    {
        var previous = _selectedImageBlock;
        if (previous is not null)
        {
            previous.IsSelected = false;
        }

        _selectedImageBlock = image;
        if (image is not null)
        {
            image.IsSelected = true;
        }

        if (!ReferenceEquals(previous, image))
        {
            SelectedImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RestoreBlocks(
        NoteDocument note,
        IReadOnlyList<NoteBlock> snapshot,
        Action<ImageNoteBlock> initializeImage)
    {
        using (SuppressChangeTracking())
        {
            note.Blocks.Clear();
            foreach (var block in CloneBlocks(snapshot))
            {
                if (block is ImageNoteBlock image) initializeImage(image);
                note.Blocks.Add(block);
            }

            NormalizeTextBlocksCore(note);
        }
    }

    private static string[] GetImageAssetNames(IEnumerable<NoteBlock> blocks) =>
        blocks.OfType<ImageNoteBlock>()
            .Select(image => image.AssetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static double ClampImageWidth(double requestedWidth, double availableWidth)
    {
        var maximum = Math.Max(MinimumImageWidth, availableWidth);
        return Math.Clamp(requestedWidth, MinimumImageWidth, maximum);
    }

    private static ImageNoteBlock CreateImageBlock(ImageBlockData image, double availableWidth)
    {
        var safeAvailableWidth = double.IsFinite(availableWidth) && availableWidth > 0
            ? availableWidth
            : DefaultAvailableImageWidth;
        return new ImageNoteBlock
        {
            AssetFileName = image.AssetFileName,
            AssetPath = image.AssetPath,
            NaturalWidth = image.NaturalWidth,
            NaturalHeight = image.NaturalHeight,
            DisplayWidth = Math.Min(image.NaturalWidth, safeAvailableWidth)
        };
    }

    private LinkListMutationResult DeleteLinkListBlockCore(
        NoteDocument note,
        LinkListNoteBlock block,
        int maximumUndoOperations)
    {
        var evictedAssets = PushUndoSnapshot(note, maximumUndoOperations);
        using (SuppressChangeTracking()) note.Blocks.Remove(block);
        NormalizeActiveTextBlocks(note);
        return new LinkListMutationResult(note, evictedAssets);
    }

    private int ResolveInsertionIndex(NoteDocument note, NoteBlock? anchor)
    {
        if (anchor is null) return note.Blocks.Count;
        if (!TryGetOwner(anchor, out var owner) || !ReferenceEquals(owner, note)) return -1;
        return note.Blocks.IndexOf(anchor);
    }

    public void Subscribe(NoteDocument note)
    {
        if (!_subscribedNotes.Add(note)) return;

        note.PropertyChanged += OnNoteChanged;
        note.Blocks.CollectionChanged += OnBlocksCollectionChanged;
        foreach (var block in note.Blocks) SubscribeBlock(note, block);
    }

    public void Unsubscribe(NoteDocument note)
    {
        if (!_subscribedNotes.Remove(note)) return;

        note.PropertyChanged -= OnNoteChanged;
        note.Blocks.CollectionChanged -= OnBlocksCollectionChanged;
        foreach (var block in note.Blocks)
        {
            block.PropertyChanged -= OnBlockChanged;
            _blockOwners.Remove(block);
        }
    }

    public bool TryGetOwner(NoteBlock block, out NoteDocument owner) =>
        _blockOwners.TryGetValue(block, out owner!);

    public IDisposable SuppressChangeTracking()
    {
        _changeTrackingSuppressionDepth++;
        return new ChangeTrackingSuppression(this);
    }

    private bool IsChangeTrackingSuppressed => _changeTrackingSuppressionDepth > 0;

    private void ReleaseChangeTrackingSuppression() => _changeTrackingSuppressionDepth--;

    private void SubscribeBlock(NoteDocument note, NoteBlock block)
    {
        _blockOwners[block] = note;
        block.PropertyChanged += OnBlockChanged;
    }

    private void OnBlocksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var note = _subscribedNotes.FirstOrDefault(candidate => ReferenceEquals(candidate.Blocks, sender));
        if (note is null) return;

        if (e.OldItems is not null)
        {
            foreach (NoteBlock block in e.OldItems)
            {
                block.PropertyChanged -= OnBlockChanged;
                _blockOwners.Remove(block);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (NoteBlock block in e.NewItems) SubscribeBlock(note, block);
        }

        if (!IsChangeTrackingSuppressed) NoteMutated?.Invoke(note);
    }

    private void OnBlockChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsChangeTrackingSuppressed
            || sender is not NoteBlock block
            || !TryGetOwner(block, out var note)) return;

        if (block is ImageNoteBlock
            && e.PropertyName is nameof(ImageNoteBlock.IsSelected)
                or nameof(ImageNoteBlock.AssetPath)
                or nameof(ImageNoteBlock.PreviewWidth)
                or nameof(ImageNoteBlock.PreviewHeight)
                or nameof(ImageNoteBlock.LayoutWidth)
                or nameof(ImageNoteBlock.LayoutHeight)) return;

        NoteMutated?.Invoke(note);
    }

    private void OnNoteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is NoteDocument note)
        {
            NotePropertyChanged?.Invoke(this, new NotePropertyChangedEventArgs(note, e));
        }
    }

    private sealed class ChangeTrackingSuppression(ActiveNoteEditor editor) : IDisposable
    {
        private ActiveNoteEditor? _editor = editor;

        public void Dispose()
        {
            var editor = Interlocked.Exchange(ref _editor, null);
            editor?.ReleaseChangeTrackingSuppression();
        }
    }
}

public sealed record TextBlockInsertionResult(TextNoteBlock Block, string[] EvictedAssets);
public sealed record TextBlockMutationResult(TextNoteBlock? FocusTarget, string[] EvictedAssets);
public sealed record ImageBlockData(
    string AssetFileName,
    string AssetPath,
    double NaturalWidth,
    double NaturalHeight);
public sealed record ImageInsertionResult(
    NoteDocument Note,
    ImageNoteBlock ImageBlock,
    TextNoteBlock TrailingTextBlock,
    string[] EvictedAssets);
public sealed record ImageDeletionResult(string AssetFileName, string[] EvictedAssets);
public sealed record ImageResizeCommitResult(string[] EvictedAssets);
public sealed record LinkListBlockInsertionResult(
    NoteDocument Note,
    LinkListNoteBlock Block,
    string[] EvictedAssets);
public sealed record LinkTokenCommitMutationResult(
    IReadOnlyList<NoteLinkItem> AcceptedItems,
    string InvalidDraft);
public sealed record LinkListMutationResult(NoteDocument Note, string[] EvictedAssets);
public sealed record LinkListItemMutationResult(NoteDocument Note, string[] EvictedAssets);
public sealed record LinkListBackspaceMutationResult(
    NoteDocument Note,
    bool BlockDeleted,
    string[] EvictedAssets);

public sealed class NotePropertyChangedEventArgs(NoteDocument note, PropertyChangedEventArgs change) : EventArgs
{
    public NoteDocument Note { get; } = note;
    public PropertyChangedEventArgs Change { get; } = change;
}
