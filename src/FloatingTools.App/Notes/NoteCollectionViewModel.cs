using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.Notes;

/// <summary>Owns Notes list, menu, rename, and note-lifecycle orchestration.</summary>
public sealed partial class NoteCollectionViewModel : ObservableObject
{
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<NoteDocument?> _activeNote;
    private readonly Func<NoteDocument, Task> _resolveCurrentForLeaving;
    private readonly Action<NoteDocument> _setActive;
    private readonly Action<NoteDocument> _initializeCreatedNote;
    private readonly Action<NoteDocument> _subscribe;
    private readonly Action<NoteDocument> _unsubscribe;
    private readonly Func<NoteDocument, bool> _isMeaningful;
    private readonly Action<NoteDocument> _updateAutomaticTitle;
    private readonly Func<NoteDocument, IEnumerable<string>> _getAssetNames;
    private readonly Func<Guid, IEnumerable<string>> _discardUndoHistory;
    private readonly Func<IEnumerable<string>, Task> _deleteUnreferencedAssets;
    private readonly Func<Task> _saveSnapshot;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isMenuOpen;
    [ObservableProperty] private NoteDocument? _renamingNote;
    [ObservableProperty] private string _renameText = string.Empty;

    public NoteCollectionViewModel(
        Func<DateTimeOffset> now,
        Func<NoteDocument?> activeNote,
        Func<NoteDocument, Task> resolveCurrentForLeaving,
        Action<NoteDocument> setActive,
        Action<NoteDocument> initializeCreatedNote,
        Action<NoteDocument> subscribe,
        Action<NoteDocument> unsubscribe,
        Func<NoteDocument, bool> isMeaningful,
        Action<NoteDocument> updateAutomaticTitle,
        Func<NoteDocument, IEnumerable<string>> getAssetNames,
        Func<Guid, IEnumerable<string>> discardUndoHistory,
        Func<IEnumerable<string>, Task> deleteUnreferencedAssets,
        Func<Task> saveSnapshot)
    {
        _now = now;
        _activeNote = activeNote;
        _resolveCurrentForLeaving = resolveCurrentForLeaving;
        _setActive = setActive;
        _initializeCreatedNote = initializeCreatedNote;
        _subscribe = subscribe;
        _unsubscribe = unsubscribe;
        _isMeaningful = isMeaningful;
        _updateAutomaticTitle = updateAutomaticTitle;
        _getAssetNames = getAssetNames;
        _discardUndoHistory = discardUndoHistory;
        _deleteUnreferencedAssets = deleteUnreferencedAssets;
        _saveSnapshot = saveSnapshot;
    }

    public ObservableCollection<NoteDocument> Notes { get; } = [];

    public IReadOnlyList<NoteDocument> FilteredNotes => Notes
        .Where(note => SearchQuery.Trim().Length == 0
            || note.Title.Contains(SearchQuery.Trim(), StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(note => note.UpdatedAt)
        .ToArray();

    partial void OnSearchQueryChanged(string value) => OnPropertyChanged(nameof(FilteredNotes));

    public void ToggleMenu()
    {
        if (IsMenuOpen) CancelRename();
        IsMenuOpen = !IsMenuOpen;
    }

    public async Task NewNoteAsync()
    {
        if (_activeNote() is { } active) await _resolveCurrentForLeaving(active);
        var note = CreateNote(false);
        EnsureSavedMembership(note);
        _setActive(note);
        IsMenuOpen = false;
        await _saveSnapshot();
    }

    public async Task NewTemporaryNoteAsync()
    {
        if (_activeNote() is { } active) await _resolveCurrentForLeaving(active);
        _setActive(CreateNote(true));
        IsMenuOpen = false;
    }

    public async Task KeepTemporaryNoteAsync()
    {
        if (_activeNote() is not { IsTemporary: true } note || !_isMeaningful(note)) return;
        note.IsTemporary = false;
        _updateAutomaticTitle(note);
        EnsureSavedMembership(note);
        OnPropertyChanged(nameof(FilteredNotes));
        await _saveSnapshot();
    }

    public async Task SelectNoteAsync(NoteDocument? note)
    {
        if (note is null || ReferenceEquals(note, _activeNote())) { IsMenuOpen = false; return; }
        if (_activeNote() is { } active) await _resolveCurrentForLeaving(active);
        _setActive(note);
        IsMenuOpen = false;
        await _saveSnapshot();
    }

    public void BeginRename(NoteDocument? note)
    {
        if (note is null) return;
        if (RenamingNote is not null) RenamingNote.IsRenaming = false;
        RenamingNote = note;
        note.IsRenaming = true;
        RenameText = note.Title;
    }

    public async Task SaveRenameAsync()
    {
        if (RenamingNote is null) return;
        if (string.IsNullOrWhiteSpace(RenameText)) { CancelRename(); return; }
        var note = RenamingNote;
        var title = RenameText.Trim();
        note.Title = title[..Math.Min(title.Length, NoteTitleGenerator.MaximumTitleLength)];
        note.HasManualTitle = true;
        note.IsTitleLocked = false;
        note.UpdatedAt = _now();
        if (!note.IsTemporary) EnsureSavedMembership(note);
        CancelRename();
        OnPropertyChanged(nameof(FilteredNotes));
        await _saveSnapshot();
    }

    public void CancelRename()
    {
        if (RenamingNote is not null) RenamingNote.IsRenaming = false;
        RenamingNote = null;
        RenameText = string.Empty;
    }

    public async Task DeleteNoteAsync(NoteDocument? note)
    {
        if (note is null || note.IsTemporary) return;
        var assets = _getAssetNames(note).Concat(_discardUndoHistory(note.Id)).ToArray();
        var deletingActive = ReferenceEquals(note, _activeNote());
        RemoveFromSavedNotes(note);
        if (deletingActive)
        {
            var next = Notes.OrderByDescending(item => item.UpdatedAt).FirstOrDefault();
            if (next is null) { next = CreateNote(false); EnsureSavedMembership(next); }
            _setActive(next);
        }
        OnPropertyChanged(nameof(FilteredNotes));
        await _saveSnapshot();
        await _deleteUnreferencedAssets(assets);
    }

    public void EnsureSavedMembership(NoteDocument note)
    {
        _subscribe(note);
        if (Notes.Contains(note)) return;

        Notes.Add(note);
        OnPropertyChanged(nameof(FilteredNotes));
    }

    public void RemoveFromSavedNotes(NoteDocument note)
    {
        if (!Notes.Remove(note)) return;

        if (!ReferenceEquals(note, _activeNote())) _unsubscribe(note);
        OnPropertyChanged(nameof(FilteredNotes));
    }

    public NoteDocument CreateNote(bool isTemporary)
    {
        var now = _now();
        var note = new NoteDocument
        {
            Id = Guid.NewGuid(),
            Title = NoteTitleGenerator.UntitledTitle,
            CreatedAt = now,
            UpdatedAt = now,
            IsTemporary = isTemporary,
            Content = null
        };
        note.Blocks.Add(ActiveNoteEditor.CreateAutomaticTextBlock());
        _initializeCreatedNote(note);
        return note;
    }
}
