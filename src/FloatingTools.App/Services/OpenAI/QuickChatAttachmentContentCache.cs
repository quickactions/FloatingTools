using System.Collections.Concurrent;

namespace FloatingTools.App.Services.OpenAI;

/// <summary>
/// In-memory, session-scoped cache of a Quick Chat image attachment's
/// prepared request content (its data: URL), keyed by the attachment's
/// immutable managed asset file name. Attachment files never change once
/// created (see <see cref="LocalQuickChatImageStore"/>), so once an
/// attachment's content has been read and Base64-encoded for one request it
/// can be reused for every later request in the same conversation, instead
/// of re-reading and re-encoding it from disk every time.
///
/// This cache is intentionally small and short-lived: nothing here is
/// persisted to disk, and callers are expected to <see cref="Clear"/> it
/// when a conversation resets and <see cref="Remove"/> a single entry when
/// its attachment is deleted, so it never outlives the images it describes.
/// </summary>
public sealed class QuickChatAttachmentContentCache
{
    private readonly ConcurrentDictionary<string, string> _dataUrlsByAssetFileName =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetDataUrl(string assetFileName, out string dataUrl) =>
        _dataUrlsByAssetFileName.TryGetValue(assetFileName, out dataUrl!);

    public void SetDataUrl(string assetFileName, string dataUrl) =>
        _dataUrlsByAssetFileName[assetFileName] = dataUrl;

    public void Remove(string assetFileName) =>
        _dataUrlsByAssetFileName.TryRemove(assetFileName, out _);

    public void Clear() => _dataUrlsByAssetFileName.Clear();
}
