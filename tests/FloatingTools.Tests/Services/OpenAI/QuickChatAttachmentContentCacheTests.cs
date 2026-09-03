using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services.OpenAI;

public sealed class QuickChatAttachmentContentCacheTests
{
    [Fact]
    public void TryGetDataUrl_MissesUntilSet()
    {
        var cache = new QuickChatAttachmentContentCache();

        Assert.False(cache.TryGetDataUrl("a.png", out _));

        cache.SetDataUrl("a.png", "data:image/png;base64,AAAA");

        Assert.True(cache.TryGetDataUrl("a.png", out var dataUrl));
        Assert.Equal("data:image/png;base64,AAAA", dataUrl);
    }

    [Fact]
    public void Remove_ClearsOnlyTheNamedEntry()
    {
        var cache = new QuickChatAttachmentContentCache();
        cache.SetDataUrl("a.png", "data:image/png;base64,AAAA");
        cache.SetDataUrl("b.png", "data:image/png;base64,BBBB");

        cache.Remove("a.png");

        Assert.False(cache.TryGetDataUrl("a.png", out _));
        Assert.True(cache.TryGetDataUrl("b.png", out _));
    }

    [Fact]
    public void Clear_RemovesEveryEntry()
    {
        var cache = new QuickChatAttachmentContentCache();
        cache.SetDataUrl("a.png", "data:image/png;base64,AAAA");
        cache.SetDataUrl("b.png", "data:image/png;base64,BBBB");

        cache.Clear();

        Assert.False(cache.TryGetDataUrl("a.png", out _));
        Assert.False(cache.TryGetDataUrl("b.png", out _));
    }

    [Fact]
    public void AssetFileNameLookup_IsCaseInsensitive()
    {
        var cache = new QuickChatAttachmentContentCache();
        cache.SetDataUrl("Asset.PNG", "data:image/png;base64,AAAA");

        Assert.True(cache.TryGetDataUrl("asset.png", out var dataUrl));
        Assert.Equal("data:image/png;base64,AAAA", dataUrl);
    }
}
