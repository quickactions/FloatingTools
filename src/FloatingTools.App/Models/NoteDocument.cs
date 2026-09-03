using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;

namespace FloatingTools.App.Models;

public partial class NoteDocument : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string _title = "Untitled note";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    public ObservableCollection<NoteBlock> Blocks { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsTemporary { get; set; }

    public bool HasManualTitle { get; set; }

    // Retained only so notes written by the first Notes MVP can be migrated safely.
    public bool IsTitleLocked { get; set; }

    [JsonIgnore]
    [ObservableProperty]
    private bool _isActive;

    [JsonIgnore]
    [ObservableProperty]
    private bool _isRenaming;
}

public sealed class NotesStorageState
{
    public List<NoteDocument> Notes { get; set; } = [];

    public Guid? LastOpenedNoteId { get; set; }
}
