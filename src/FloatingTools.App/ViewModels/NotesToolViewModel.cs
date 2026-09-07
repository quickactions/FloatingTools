using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Notes;
using FloatingTools.App.Services;

namespace FloatingTools.App.ViewModels;

public sealed record LinkTokenCommitResult(
    IReadOnlyList<NoteLinkItem> AcceptedItems,
    string InvalidDraft);

public partial class NotesToolViewModel : ObservableObject
{
    public const double MinimumImageWidth = 60;
    private const int MaximumUndoOperationsPerNote = 30;

    private readonly INotesStore _store;
    private readonly INotesImageStore _imageStore;
    private readonly IClipboardService _clipboardService;
    private readonly INoteLinkLauncher _linkLauncher;
    private readonly INoteExportService _exportService;
    private readonly Func<DateTimeOffset> _now;
    private readonly NoteCollectionViewModel _collection;
    private readonly ActiveNoteEditor _activeNoteEditor;
    private readonly ObservableCollection<NoteBlock> _emptyBlocks = [];

    [ObservableProperty]
    private NoteDocument? _activeNote;

    public NotesToolViewModel(
        INotesStore store,
        TimeSpan? debounceDelay = null,
        Func<DateTimeOffset>? now = null,
        INotesImageStore? imageStore = null,
        IClipboardService? clipboardService = null,
        INoteLinkLauncher? linkLauncher = null,
        INoteExportService? exportService = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        var resolvedDebounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(850);
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _activeNoteEditor = new ActiveNoteEditor(resolvedDebounceDelay);
        _imageStore = imageStore ?? new NullNotesImageStore();
        _clipboardService = clipboardService ?? new NullClipboardService();
        _linkLauncher = linkLauncher ?? new NullNoteLinkLauncher();
        _exportService = exportService ?? new NullNoteExportService();
        _activeNoteEditor.NoteMutated += note => MarkNoteChanged(note);
        _activeNoteEditor.NotePropertyChanged += OnTrackedNoteChanged;
        _activeNoteEditor.UndoHistoryChanged += (_, _) => OnPropertyChanged(nameof(CanUndoDocumentOperation));
        _activeNoteEditor.SelectedImageChanged += (_, _) => OnPropertyChanged(nameof(SelectedImageBlock));
        _collection = new NoteCollectionViewModel(
            _now, () => ActiveNote, _ => ResolveCurrentForLeavingAsync(), SetActive,
            note =>
            {
                InitializeImagePaths(note);
                _activeNoteEditor.Subscribe(note);
            },
            _activeNoteEditor.Subscribe, _activeNoteEditor.Unsubscribe, IsMeaningful,
            UpdateAutomaticTitle, _activeNoteEditor.GetImageAssetNames, _activeNoteEditor.DiscardUndoHistory,
            assets => DeleteUnreferencedAssetsAsync(assets), () => SaveSnapshotAsync());
        _collection.PropertyChanged += (_, args) =>
        {
            OnPropertyChanged(args.PropertyName);
            if (args.PropertyName is nameof(SearchQuery) or nameof(NoteCollectionViewModel.FilteredNotes))
                OnPropertyChanged(nameof(FilteredNotes));
            if (args.PropertyName is nameof(NoteCollectionViewModel.RenamingNote))
                OnPropertyChanged(nameof(RenamingNote));
        };
    }

    public ObservableCollection<NoteDocument> Notes => _collection.Notes;
    public string SearchQuery { get => _collection.SearchQuery; set => _collection.SearchQuery = value; }
    public bool IsMenuOpen { get => _collection.IsMenuOpen; set => _collection.IsMenuOpen = value; }
    public NoteDocument? RenamingNote { get => _collection.RenamingNote; set => _collection.RenamingNote = value; }
    public string RenameText { get => _collection.RenameText; set => _collection.RenameText = value; }

    public ObservableCollection<NoteBlock> ActiveBlocks =>
        ActiveNote?.Blocks ?? _emptyBlocks;

    public ImageNoteBlock? SelectedImageBlock
    {
        get => _activeNoteEditor.SelectedImageBlock;
        set => _activeNoteEditor.SelectImage(value);
    }

    public bool CanUndoDocumentOperation => ActiveNote is { } note && _activeNoteEditor.HasUndoHistory(note);

    // Compatibility surface for the existing plain-text ViewModel tests and callers.
    // Rich Notes UI binds directly to individual TextNoteBlock instances.
    public string Content
    {
        get => ActiveNote is null ? string.Empty : GetTextContent(ActiveNote);
        set
        {
            if (ActiveNote is null || value == Content)
            {
                return;
            }

            var textBlock = ActiveNote.Blocks.OfType<TextNoteBlock>().FirstOrDefault();
            if (textBlock is null)
            {
                textBlock = new TextNoteBlock();
                ActiveNote.Blocks.Insert(0, textBlock);
            }

            textBlock.Text = value;
        }
    }

    public IReadOnlyList<NoteDocument> FilteredNotes => _collection.FilteredNotes;

    public bool IsTemporary => ActiveNote?.IsTemporary == true;

    public string ActiveTitle => ActiveNote switch
    {
        { IsTemporary: true } => "Temporary note",
        { } note => note.Title,
        _ => NoteTitleGenerator.UntitledTitle
    };

    public static bool ShouldPersist(NoteDocument note) =>
        !note.IsTemporary
        && (note.HasManualTitle
            || note.Blocks.OfType<TextNoteBlock>().Any(block =>
                !string.IsNullOrWhiteSpace(block.Text))
        || note.Blocks.OfType<ImageNoteBlock>().Any(block =>
            !string.IsNullOrWhiteSpace(block.AssetFileName))
        || note.Blocks.OfType<LinkListNoteBlock>().Any(block => block.Items.Count > 0));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var state = await _store.LoadAsync(cancellationToken);
        foreach (var note in state.Notes)
        {
            InitializeImagePaths(note);
            if (!ShouldPersist(note))
            {
                continue;
            }

            _activeNoteEditor.Subscribe(note);
            Notes.Add(note);
        }

        var active = state.LastOpenedNoteId is Guid id
            ? Notes.FirstOrDefault(note => note.Id == id)
            : null;
        active ??= Notes.OrderByDescending(note => note.UpdatedAt).FirstOrDefault();
        if (active is null)
        {
            active = CreateNote(isTemporary: false);
            EnsureSavedMembership(active);
        }

        SetActive(active);
        await SaveSnapshotAsync(cancellationToken);
    }

    [RelayCommand]
    private void ToggleMenu() => _collection.ToggleMenu();

    [RelayCommand]
    private Task NewNoteAsync() => _collection.NewNoteAsync();

    [RelayCommand]
    private Task NewTemporaryNoteAsync() => _collection.NewTemporaryNoteAsync();

    [RelayCommand]
    private async Task KeepTemporaryNoteAsync()
    {
        await _collection.KeepTemporaryNoteAsync();
        OnPropertyChanged(nameof(IsTemporary));
        OnPropertyChanged(nameof(ActiveTitle));
    }

    [RelayCommand]
    private Task SelectNoteAsync(NoteDocument? note) => _collection.SelectNoteAsync(note);

    [RelayCommand]
    private void BeginRename(NoteDocument? note) => _collection.BeginRename(note);

    [RelayCommand]
    private async Task SaveRenameAsync()
    {
        await _collection.SaveRenameAsync();
        OnPropertyChanged(nameof(ActiveTitle));
    }

    [RelayCommand]
    private void CancelRename() => _collection.CancelRename();

    [RelayCommand]
    private Task DeleteNoteAsync(NoteDocument? note) => _collection.DeleteNoteAsync(note);

    [RelayCommand]
    private Task ExportNotePdfAsync(NoteDocument? note) =>
        ExportNoteAsync(note, NoteExportFormat.Pdf);

    [RelayCommand]
    private Task ExportNoteWordAsync(NoteDocument? note) =>
        ExportNoteAsync(note, NoteExportFormat.Word);

    private async Task ExportNoteAsync(NoteDocument? note, NoteExportFormat format)
    {
        if (note is null)
        {
            return;
        }

        await _exportService.ExportAsync(note, format);
    }

    public async Task<TextNoteBlock?> InsertClipboardImageAsync(
        TextNoteBlock target,
        int selectionStart,
        int selectionLength,
        byte[] pngBytes,
        double availableWidth,
        CancellationToken cancellationToken = default)
    {
        var image = await Task.Run(
            () => _imageStore.ImportPngAsync(pngBytes, cancellationToken),
            cancellationToken);
        return await InsertManagedImageIntoTextAsync(
            target,
            selectionStart,
            selectionLength,
            image,
            availableWidth,
            cancellationToken);
    }

    public async Task<TextNoteBlock?> InsertClipboardImageAfterBlockAsync(
        NoteBlock target,
        byte[] pngBytes,
        double availableWidth,
        CancellationToken cancellationToken = default)
    {
        var image = await Task.Run(
            () => _imageStore.ImportPngAsync(pngBytes, cancellationToken),
            cancellationToken);
        return await InsertManagedImageAfterBlockAsync(
            target,
            image,
            availableWidth,
            cancellationToken);
    }

    public async Task<TextNoteBlock?> InsertImageFileAsync(
        NoteBlock target,
        int? textPosition,
        string sourcePath,
        double availableWidth,
        CancellationToken cancellationToken = default)
    {
        var image = await Task.Run(
            () => _imageStore.ImportFileAsync(sourcePath, cancellationToken),
            cancellationToken);
        if (target is TextNoteBlock textBlock)
        {
            return await InsertManagedImageIntoTextAsync(
                textBlock,
                textPosition ?? textBlock.Text.Length,
                0,
                image,
                availableWidth,
                cancellationToken);
        }

        return await InsertManagedImageAfterBlockAsync(
            target,
            image,
            availableWidth,
            cancellationToken);
    }

    public void SelectImage(ImageNoteBlock? image)
    {
        _activeNoteEditor.SelectImage(image);
    }

    public void CopyTextBlock(TextNoteBlock block) =>
        _clipboardService.SetText(block.Text);

    public async Task<LinkListNoteBlock?> InsertLinkListBlockBeforeAsync(
        NoteBlock? anchor,
        CancellationToken cancellationToken = default)
    {
        if (ActiveNote is not { } note) return null;
        var result = _activeNoteEditor.InsertLinkListBlock(
            note, anchor, MaximumUndoOperationsPerNote);
        if (result is null) return null;

        MarkNoteChanged(result.Note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
        return result.Block;
    }

    public LinkTokenCommitResult CommitLinkTokens(LinkListNoteBlock block, string input)
    {
        var result = _activeNoteEditor.CommitLinkTokens(
            block, input, MaximumUndoOperationsPerNote);
        return new(result.AcceptedItems, result.InvalidDraft);
    }

    public void CopyLinkItem(NoteLinkItem item) => _clipboardService.SetText(item.Url);

    public Task<bool> OpenLinkItemAsync(NoteLinkItem item, CancellationToken cancellationToken = default) =>
        _linkLauncher.TryOpenAsync(item.Url, cancellationToken);

    public async Task<bool> EditLinkItemAsync(
        LinkListNoteBlock block,
        NoteLinkItem item,
        string displayName,
        string url,
        CancellationToken cancellationToken = default)
    {
        if (_activeNoteEditor.EditLinkItem(
                block, item, displayName, url, MaximumUndoOperationsPerNote) is null) return false;
        await SaveNowAsync(cancellationToken);
        return true;
    }

    public async Task DeleteLinkItemAsync(
        LinkListNoteBlock block,
        NoteLinkItem item,
        CancellationToken cancellationToken = default)
    {
        if (_activeNoteEditor.DeleteLinkItem(block, item, MaximumUndoOperationsPerNote) is null) return;
        await SaveNowAsync(cancellationToken);
    }

    public async Task<bool> DeleteLastLinkItemOrBlockAsync(
        LinkListNoteBlock block,
        CancellationToken cancellationToken = default)
    {
        var result = _activeNoteEditor.DeleteLastLinkItemOrBlock(
            block, MaximumUndoOperationsPerNote);
        if (result is null) return false;

        if (result.BlockDeleted)
        {
            MarkNoteChanged(result.Note, scheduleSave: false);
            await SaveSnapshotAsync(cancellationToken);
            await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
        }
        else
        {
            await SaveNowAsync(cancellationToken);
        }

        return true;
    }

    public void CopyLinkListBlock(LinkListNoteBlock block) =>
        _clipboardService.SetText(string.Join(Environment.NewLine, block.Items.Select(item => item.Url)));

    public async Task CutLinkListBlockAsync(LinkListNoteBlock block, CancellationToken cancellationToken = default)
    {
        CopyLinkListBlock(block);
        await DeleteLinkListBlockAsync(block, cancellationToken);
    }

    public async Task DeleteLinkListBlockAsync(LinkListNoteBlock block, CancellationToken cancellationToken = default)
    {
        var result = _activeNoteEditor.DeleteLinkListBlock(block, MaximumUndoOperationsPerNote);
        if (result is null) return;
        MarkNoteChanged(result.Note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
    }

    public async Task<TextNoteBlock?> CutTextBlockAsync(
        TextNoteBlock block,
        CancellationToken cancellationToken = default)
    {
        CopyTextBlock(block);
        return await DeleteTextBlockAsync(block, cancellationToken);
    }

    public async Task<TextNoteBlock?> DeleteTextBlockAsync(
        TextNoteBlock block,
        CancellationToken cancellationToken = default)
    {
        if (ActiveNote is not { } note) return null;
        var result = _activeNoteEditor.DeleteTextBlock(note, block, MaximumUndoOperationsPerNote);
        if (result is null) return null;
        MarkNoteChanged(note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
        return result.FocusTarget;
    }

    public async Task<TextNoteBlock?> InsertTextBlockBeforeAsync(
        NoteBlock? anchor,
        CancellationToken cancellationToken = default)
    {
        if (ActiveNote is not { } note) return null;
        var result = _activeNoteEditor.InsertTextBlock(note, anchor, MaximumUndoOperationsPerNote);
        if (result is null) return null;

        MarkNoteChanged(note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
        return result.Block;
    }

    public void ResizeImage(ImageNoteBlock image, double requestedWidth, double availableWidth)
    {
        _activeNoteEditor.ResizeImage(image, requestedWidth, availableWidth);
    }

    public async Task CommitImageResizeAsync(
        ImageNoteBlock image,
        double requestedWidth,
        double availableWidth,
        CancellationToken cancellationToken = default)
    {
        if (ActiveNote is not { } note)
        {
            return;
        }

        var result = _activeNoteEditor.CommitImageResize(
            note, image, requestedWidth, availableWidth, MaximumUndoOperationsPerNote);
        if (result is null) return;
        await SaveNowAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
    }

    public TextNoteBlock EnsureFinalEditableTextBlock()
    {
        if (ActiveNote is not { } note)
        {
            throw new InvalidOperationException("No active note is available.");
        }

        if (note.Blocks.LastOrDefault() is TextNoteBlock finalText)
        {
            return finalText;
        }

        var block = note.Blocks.OfType<TextNoteBlock>().Any()
            ? new TextNoteBlock()
            : ActiveNoteEditor.CreateAutomaticTextBlock();
        note.Blocks.Add(block);
        return block;
    }

    public async Task NormalizeActiveTextBlocksAsync(
        CancellationToken cancellationToken = default)
    {
        if (ActiveNote is not { } note || !_activeNoteEditor.NormalizeActiveTextBlocks(note))
        {
            return;
        }

        MarkNoteChanged(note, scheduleSave: false);
        if (!note.IsTemporary)
        {
            await SaveSnapshotAsync(cancellationToken);
        }
    }

    public async Task<TextNoteBlock?> MergeEmptyTextBlockBackwardAsync(
        TextNoteBlock emptyBlock,
        CancellationToken cancellationToken = default)
    {
        if (ActiveNote is not { } note) return null;
        var result = _activeNoteEditor.MergeEmptyTextBlockBackward(note, emptyBlock, MaximumUndoOperationsPerNote);
        if (result is null) return null;

        MarkNoteChanged(note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
        return result.FocusTarget;
    }

    public async Task UndoDocumentOperationAsync(
        CancellationToken cancellationToken = default)
    {
        if (ActiveNote is not { } note
            || !_activeNoteEditor.TryPopUndoSnapshot(note, out var record))
        {
            return;
        }

        CancelDebounce();
        var currentAssets = _activeNoteEditor.GetImageAssetNames(note).ToArray();
        _activeNoteEditor.RestoreBlocks(
            note,
            record.Blocks,
            image => image.AssetPath = _imageStore.GetAbsolutePath(image.AssetFileName));
        OnPropertyChanged(nameof(ActiveBlocks));
        OnPropertyChanged(nameof(Content));
        SelectImage(null);
        MarkNoteChanged(note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(currentAssets, cancellationToken);
        OnPropertyChanged(nameof(CanUndoDocumentOperation));
    }

    public async Task DeleteSelectedImageAsync(CancellationToken cancellationToken = default)
    {
        if (ActiveNote is null || SelectedImageBlock is not { } image)
        {
            return;
        }

        var result = _activeNoteEditor.DeleteImage(
            ActiveNote, image, MaximumUndoOperationsPerNote);
        if (result is null) return;
        MarkNoteChanged(ActiveNote, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(
            result.EvictedAssets.Append(result.AssetFileName),
            cancellationToken);
    }

    public async Task SaveNowAsync(CancellationToken cancellationToken = default)
    {
        CancelDebounce();
        if (ActiveNote is not { IsTemporary: false } note)
        {
            return;
        }

        UpdateAutomaticTitle(note);
        if (ShouldPersist(note))
        {
            EnsureSavedMembership(note);
        }

        await SaveSnapshotAsync(cancellationToken);
    }

    public async Task PrepareForExitAsync(CancellationToken cancellationToken = default)
    {
        var undoAssets = _activeNoteEditor.DiscardAllUndoHistory();
        if (ActiveNote is { IsTemporary: true } temporary)
        {
            var assets = _activeNoteEditor.GetImageAssetNames(temporary).Concat(undoAssets).ToArray();
            _activeNoteEditor.Unsubscribe(temporary);
            await DeleteUnreferencedAssetsAsync(assets, cancellationToken, temporary);
            return;
        }

        await SaveNowAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(undoAssets, cancellationToken);
    }

    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        IsMenuOpen = false;
        await ResolveCurrentForLeavingAsync(cancellationToken);

        if (ActiveNote is null || !ShouldPersist(ActiveNote))
        {
            var next = Notes.OrderByDescending(note => note.UpdatedAt).FirstOrDefault();
            if (next is null)
            {
                next = CreateNote(isTemporary: false);
                EnsureSavedMembership(next);
            }

            SetActive(next);
        }

        await SaveSnapshotAsync(cancellationToken);
    }

    private async Task<TextNoteBlock?> InsertManagedImageIntoTextAsync(
        TextNoteBlock target,
        int selectionStart,
        int selectionLength,
        ManagedNoteImage managed,
        double availableWidth,
        CancellationToken cancellationToken)
    {
        if (ActiveNote is not { } note)
        {
            await DeleteImportedAssetAsync(managed.AssetFileName, cancellationToken);
            return null;
        }

        var result = _activeNoteEditor.InsertImageIntoText(
            note,
            target,
            selectionStart,
            selectionLength,
            new ImageBlockData(
                managed.AssetFileName,
                managed.AbsolutePath,
                managed.NaturalWidth,
                managed.NaturalHeight),
            availableWidth,
            MaximumUndoOperationsPerNote);
        if (result is null)
        {
            await DeleteImportedAssetAsync(managed.AssetFileName, cancellationToken);
            return null;
        }

        MarkNoteChanged(result.Note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
        return result.FocusTarget;
    }

    private async Task<TextNoteBlock?> InsertManagedImageAfterBlockAsync(
        NoteBlock target,
        ManagedNoteImage managed,
        double availableWidth,
        CancellationToken cancellationToken)
    {
        if (ActiveNote is not { } note)
        {
            await DeleteImportedAssetAsync(managed.AssetFileName, cancellationToken);
            return null;
        }

        var result = _activeNoteEditor.InsertImageAfterBlock(
            note,
            target,
            new ImageBlockData(
                managed.AssetFileName,
                managed.AbsolutePath,
                managed.NaturalWidth,
                managed.NaturalHeight),
            availableWidth,
            MaximumUndoOperationsPerNote);
        if (result is null)
        {
            await DeleteImportedAssetAsync(managed.AssetFileName, cancellationToken);
            return null;
        }

        MarkNoteChanged(result.Note, scheduleSave: false);
        await SaveSnapshotAsync(cancellationToken);
        await DeleteUnreferencedAssetsAsync(result.EvictedAssets, cancellationToken);
        return result.FocusTarget;
    }

    private void SetActive(NoteDocument note)
    {
        CancelDebounce();
        var previous = ActiveNote;
        if (previous is not null)
        {
            previous.IsActive = false;
        }

        _activeNoteEditor.Subscribe(note);
        ActiveNote = note;
        note.IsActive = true;
        SelectImage(null);
        OnPropertyChanged(nameof(ActiveBlocks));
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(ActiveTitle));
        OnPropertyChanged(nameof(IsTemporary));
        OnPropertyChanged(nameof(FilteredNotes));
        OnPropertyChanged(nameof(CanUndoDocumentOperation));

        if (previous is not null
            && !ReferenceEquals(previous, note)
            && !Notes.Contains(previous))
        {
            _activeNoteEditor.Unsubscribe(previous);
        }
    }

    private async Task ResolveCurrentForLeavingAsync(CancellationToken cancellationToken = default)
    {
        CancelDebounce();
        if (ActiveNote is null)
        {
            return;
        }

        if (ActiveNote.IsTemporary)
        {
            var temporary = ActiveNote;
            var assets = _activeNoteEditor.GetImageAssetNames(temporary)
                .Concat(_activeNoteEditor.DiscardUndoHistory(temporary.Id))
                .ToArray();
            _activeNoteEditor.Unsubscribe(temporary);
            await DeleteUnreferencedAssetsAsync(assets, cancellationToken, temporary);
            return;
        }

        UpdateAutomaticTitle(ActiveNote);
        if (ShouldPersist(ActiveNote))
        {
            EnsureSavedMembership(ActiveNote);
        }
        else
        {
            RemoveFromSavedNotes(ActiveNote);
        }

        await SaveSnapshotAsync(cancellationToken);
    }

    private void MarkNoteChanged(NoteDocument note, bool scheduleSave = true)
    {
        note.UpdatedAt = _now();
        UpdateAutomaticTitle(note);
        if (!note.IsTemporary && ShouldPersist(note))
        {
            EnsureSavedMembership(note);
        }

        if (ReferenceEquals(note, ActiveNote))
        {
            OnPropertyChanged(nameof(Content));
            OnPropertyChanged(nameof(ActiveTitle));
            OnPropertyChanged(nameof(ActiveBlocks));
        }

        OnPropertyChanged(nameof(FilteredNotes));
        if (scheduleSave && !note.IsTemporary)
        {
            ScheduleSave();
        }
    }

    private void ScheduleSave()
    {
        _ = _activeNoteEditor.ScheduleDebouncedSave(SaveScheduledSnapshotAsync);
    }

    private async Task SaveScheduledSnapshotAsync(CancellationToken cancellationToken)
    {
        if (ActiveNote is { IsTemporary: false } note)
        {
            UpdateAutomaticTitle(note);
            if (ShouldPersist(note))
            {
                EnsureSavedMembership(note);
            }

            await SaveSnapshotAsync(cancellationToken);
        }
    }

    private async Task SaveSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var persistedNotes = Notes.Where(ShouldPersist).ToList();
        var state = new NotesStorageState
        {
            Notes = persistedNotes,
            LastOpenedNoteId = ActiveNote is { } note && ShouldPersist(note)
                ? note.Id
                : null
        };
        await _store.SaveAsync(state, cancellationToken);
    }

    private void UpdateAutomaticTitle(NoteDocument note)
    {
        note.Title = NoteTitlePolicy.ResolveTitle(note);
        if (note.HasManualTitle) return;
        note.IsTitleLocked = false;
        if (ReferenceEquals(note, ActiveNote))
        {
            OnPropertyChanged(nameof(ActiveTitle));
        }

        OnPropertyChanged(nameof(FilteredNotes));
    }

    private void EnsureSavedMembership(NoteDocument note)
    {
        _collection.EnsureSavedMembership(note);
    }

    private void RemoveFromSavedNotes(NoteDocument note)
    {
        _collection.RemoveFromSavedNotes(note);
    }

    private NoteDocument CreateNote(bool isTemporary) => _collection.CreateNote(isTemporary);

    private void InitializeImagePaths(NoteDocument note)
    {
        foreach (var image in note.Blocks.OfType<ImageNoteBlock>())
        {
            image.AssetPath = _imageStore.GetAbsolutePath(image.AssetFileName);
            if (!double.IsFinite(image.DisplayWidth) || image.DisplayWidth <= 0)
            {
                image.DisplayWidth = image.NaturalWidth > 0
                    ? image.NaturalWidth
                    : 240;
            }
        }
    }

    private static string GetTextContent(NoteDocument note) =>
        string.Join(
            Environment.NewLine,
            note.Blocks.OfType<TextNoteBlock>().Select(block => block.Text));

    private static bool IsMeaningful(NoteDocument note) =>
        note.HasManualTitle
        || note.Blocks.OfType<TextNoteBlock>().Any(block =>
            !string.IsNullOrWhiteSpace(block.Text))
        || note.Blocks.OfType<ImageNoteBlock>().Any()
        || note.Blocks.OfType<LinkListNoteBlock>().Any(block => block.Items.Count > 0);

    private HashSet<string> GetReferencedAssetNames(NoteDocument? excludedNote = null) =>
        Notes.Where(note => !ReferenceEquals(note, excludedNote))
            .SelectMany(_activeNoteEditor.GetImageAssetNames)
            .Concat(ActiveNote is not null && !ReferenceEquals(ActiveNote, excludedNote)
                ? _activeNoteEditor.GetImageAssetNames(ActiveNote)
                : [])
            .Concat(_activeNoteEditor.GetReferencedUndoAssetNames())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async Task DeleteUnreferencedAssetsAsync(
        IEnumerable<string> candidates,
        CancellationToken cancellationToken = default,
        NoteDocument? excludedNote = null)
    {
        var referenced = GetReferencedAssetNames(excludedNote);
        foreach (var asset in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!referenced.Contains(asset))
            {
                await DeleteImportedAssetAsync(asset, cancellationToken);
            }
        }
    }

    private async Task DeleteImportedAssetAsync(
        string asset,
        CancellationToken cancellationToken)
    {
        await Task.Run(
            () => _imageStore.DeleteAsync(asset, cancellationToken),
            cancellationToken);
    }

    private void OnTrackedNoteChanged(object? sender, NotePropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.Note, ActiveNote) && e.Change.PropertyName == nameof(NoteDocument.Title))
        {
            OnPropertyChanged(nameof(ActiveTitle));
        }

        OnPropertyChanged(nameof(FilteredNotes));
    }

    private void CancelDebounce()
    {
        _activeNoteEditor.CancelPendingSave();
    }

}
