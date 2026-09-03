using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FloatingTools.App.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextNoteBlock), "text")]
[JsonDerivedType(typeof(ImageNoteBlock), "image")]
[JsonDerivedType(typeof(LinkListNoteBlock), "linkList")]
[JsonDerivedType(typeof(LinkNoteBlock), "link")]
public abstract partial class NoteBlock : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public partial class TextNoteBlock : NoteBlock
{
    [ObservableProperty]
    private string _text = string.Empty;

    public bool PreserveBoundaryBefore { get; set; }

    // Persistence compatibility metadata, read and discarded by migration.
    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NoteHyperlink>? LegacyLinks { get; set; }
}

public sealed class NoteHyperlink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Start { get; set; }
    public int Length { get; set; }
    public string Url { get; set; } = string.Empty;
    [JsonIgnore] public int End => Start + Length;
}

public partial class ImageNoteBlock : NoteBlock
{
    public string AssetFileName { get; set; } = string.Empty;

    public double NaturalWidth { get; set; }

    public double NaturalHeight { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LayoutWidth))]
    [NotifyPropertyChangedFor(nameof(LayoutHeight))]
    private double _displayWidth;

    [property: JsonIgnore]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LayoutWidth))]
    private double? _previewWidth;

    [property: JsonIgnore]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LayoutHeight))]
    private double? _previewHeight;

    [property: JsonIgnore]
    [ObservableProperty]
    private string _assetPath = string.Empty;

    [property: JsonIgnore]
    [ObservableProperty]
    private bool _isSelected;

    [JsonIgnore]
    public double AspectRatio => NaturalWidth > 0 && NaturalHeight > 0
        ? NaturalWidth / NaturalHeight
        : 1;

    [JsonIgnore]
    public double LayoutWidth => PreviewWidth is { } preview
        && double.IsFinite(preview)
        && preview > 0
            ? preview
            : DisplayWidth;

    [JsonIgnore]
    public double LayoutHeight => PreviewHeight is { } preview
        && double.IsFinite(preview)
        && preview > 0
            ? preview
            : LayoutWidth / AspectRatio;

    public void SetResizePreview(double? width)
    {
        if (width is not { } value || !double.IsFinite(value) || value <= 0)
        {
            PreviewWidth = null;
            PreviewHeight = null;
            return;
        }

        PreviewWidth = value;
        PreviewHeight = value / AspectRatio;
    }

}

// Persistence compatibility DTO only. NotesMigrationPipeline converts every
// instance to plain TextNoteBlock content immediately after deserialization.
public sealed class LinkNoteBlock : NoteBlock
{
    public string Url { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    // Read-only compatibility fields for persisted formats that predate the
    // one-link-per-block model. They are cleared during document migration.
    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LegacyLinkNoteItem>? LegacyLinks { get; set; }

    [JsonPropertyName("displayText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyDisplayText { get; set; }

}

public sealed class LegacyLinkNoteItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
