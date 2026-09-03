using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services.OpenAI;

public sealed class QuickChatOpenAiRequestBuilderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FloatingTools.Tests",
        Guid.NewGuid().ToString("N"));

    public QuickChatOpenAiRequestBuilderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task WithoutACache_EveryBuildRereadsTheImageFromDisk()
    {
        var imageStore = new CountingImageStore(_directory);
        var attachment = CreateImageAttachment("a.png");
        await File.WriteAllBytesAsync(Path.Combine(_directory, "a.png"), [1, 2, 3]);
        var conversation = ConversationWithOneUserImageMessage(attachment);
        var builder = new QuickChatOpenAiRequestBuilder(imageStore);

        await builder.BuildAsync(conversation, "gpt-test");
        await builder.BuildAsync(conversation, "gpt-test");

        Assert.Equal(2, imageStore.GetAbsolutePathCallCount);
    }

    [Fact]
    public async Task WithACache_ASecondBuildForTheSameAttachmentDoesNotRereadTheFile()
    {
        var imageStore = new CountingImageStore(_directory);
        var cache = new QuickChatAttachmentContentCache();
        var attachment = CreateImageAttachment("a.png");
        var filePath = Path.Combine(_directory, "a.png");
        await File.WriteAllBytesAsync(filePath, [1, 2, 3]);
        var conversation = ConversationWithOneUserImageMessage(attachment);
        var builder = new QuickChatOpenAiRequestBuilder(imageStore, cache);

        var first = await builder.BuildAsync(conversation, "gpt-test");

        // Prove the second build truly reuses the cache rather than
        // coincidentally succeeding: delete the file it would otherwise
        // need to re-read.
        File.Delete(filePath);
        var second = await builder.BuildAsync(conversation, "gpt-test");

        var firstImageUrl = ExtractImageUrl(first);
        var secondImageUrl = ExtractImageUrl(second);
        Assert.Equal(firstImageUrl, secondImageUrl);
        Assert.StartsWith("data:image/png;base64,", firstImageUrl);
    }

    [Fact]
    public async Task CachedContent_IsIdenticalToAFreshlyComputedEncode()
    {
        var imageStore = new CountingImageStore(_directory);
        var cache = new QuickChatAttachmentContentCache();
        var attachment = CreateImageAttachment("a.png");
        var bytes = new byte[] { 10, 20, 30, 40, 50 };
        await File.WriteAllBytesAsync(Path.Combine(_directory, "a.png"), bytes);
        var conversation = ConversationWithOneUserImageMessage(attachment);
        var cachedBuilder = new QuickChatOpenAiRequestBuilder(imageStore, cache);
        var freshBuilder = new QuickChatOpenAiRequestBuilder(
            new CountingImageStore(_directory));

        var cachedResult = await cachedBuilder.BuildAsync(conversation, "gpt-test");
        var freshResult = await freshBuilder.BuildAsync(conversation, "gpt-test");

        Assert.Equal(ExtractImageUrl(freshResult), ExtractImageUrl(cachedResult));
    }

    private static string ExtractImageUrl(System.Text.Json.Nodes.JsonObject request) =>
        request["input"]![0]!["content"]![0]!["image_url"]!.GetValue<string>();

    private static QuickChatAttachment CreateImageAttachment(string assetFileName) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = QuickChatAttachmentType.Image,
            AssetFileName = assetFileName,
            MediaType = "image/png",
            Width = 10,
            Height = 10
        };

    private static QuickChatConversationState ConversationWithOneUserImageMessage(
        QuickChatAttachment attachment) =>
        new()
        {
            Messages =
            [
                new QuickChatMessage
                {
                    Id = Guid.NewGuid(),
                    Role = QuickChatMessageRole.User,
                    Attachments = [attachment],
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]
        };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class CountingImageStore(string directory) : IQuickChatImageStore
    {
        public int GetAbsolutePathCallCount { get; private set; }

        public Task<ManagedQuickChatImage> ImportFileAsync(
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ManagedQuickChatImage> ImportBytesAsync(
            ReadOnlyMemory<byte> imageBytes,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string GetAbsolutePath(string assetFileName)
        {
            GetAbsolutePathCallCount++;
            return Path.Combine(directory, assetFileName);
        }

        public IReadOnlyList<string> GetManagedAssetFileNames() => [];

        public Task DeleteAsync(
            string assetFileName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
