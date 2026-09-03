using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class OpenAiQuickChatServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FloatingTools.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Request_IncludesFixedInstructionWithoutMutatingOrPersistingIt()
    {
        var state = State(User("Hello"));
        var before = JsonSerializer.Serialize(state);

        var request = await CreateBuilder().BuildAsync(state, "quick-chat-model");

        Assert.Equal(
            QuickChatOpenAiRequestBuilder.ProductInstruction,
            request["instructions"]!.GetValue<string>());
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.DoesNotContain(
            QuickChatOpenAiRequestBuilder.ProductInstruction,
            JsonSerializer.Serialize(state));
        Assert.Equal("quick-chat-model", request["model"]!.GetValue<string>());
        Assert.True(request["stream"]!.GetValue<bool>());
        Assert.False(request["store"]!.GetValue<bool>());
        Assert.Null(request["reasoning"]);
    }

    [Fact]
    public async Task AdditionalInstructions_AreKeptSeparateAndIncludedWhenNonEmpty()
    {
        var state = State(User("Hello"));
        state.AdditionalInstructions = "  Always answer in Hebrew.  ";

        var request = await CreateBuilder().BuildAsync(state, "model");
        var instructions = request["instructions"]!.GetValue<string>();

        Assert.StartsWith(QuickChatOpenAiRequestBuilder.ProductInstruction, instructions);
        Assert.Contains("Additional user instructions:", instructions);
        Assert.EndsWith("Always answer in Hebrew.", instructions);
        Assert.Equal("  Always answer in Hebrew.  ", state.AdditionalInstructions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WhitespaceAdditionalInstructions_AreIgnored(string? additional)
    {
        var state = State(User("Hello"));
        state.AdditionalInstructions = additional;

        var request = await CreateBuilder().BuildAsync(state, "model");

        Assert.Equal(
            QuickChatOpenAiRequestBuilder.ProductInstruction,
            request["instructions"]!.GetValue<string>());
    }

    [Fact]
    public async Task Conversation_PreservesOrderAndAppliesAssistantStatusPolicy()
    {
        var failed = Assistant("failed content", QuickChatMessageStatus.Error);
        failed.ErrorMessage = "secret UI error";
        var state = State(
            User("first user"),
            Assistant("completed answer", QuickChatMessageStatus.Completed),
            Assistant("partial answer", QuickChatMessageStatus.Interrupted),
            failed,
            User("second user"),
            Assistant("not ready", QuickChatMessageStatus.InProgress));

        var request = await CreateBuilder().BuildAsync(state, "model");
        var input = request["input"]!.AsArray();

        Assert.Equal(4, input.Count);
        Assert.Equal("user", input[0]!["role"]!.GetValue<string>());
        Assert.Equal(
            "first user",
            input[0]!["content"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("assistant", input[1]!["role"]!.GetValue<string>());
        Assert.Equal("completed answer", input[1]!["content"]!.GetValue<string>());
        Assert.Equal("assistant", input[2]!["role"]!.GetValue<string>());
        Assert.Equal("partial answer", input[2]!["content"]!.GetValue<string>());
        Assert.Equal("user", input[3]!["role"]!.GetValue<string>());
        Assert.DoesNotContain("failed content", request.ToJsonString());
        Assert.DoesNotContain("secret UI error", request.ToJsonString());
        Assert.DoesNotContain("not ready", request.ToJsonString());
        Assert.DoesNotContain(state.Messages[0].Id.ToString(), request.ToJsonString());
        Assert.DoesNotContain("createdAt", request.ToJsonString());
    }

    [Fact]
    public async Task Images_UseDataUrlsForPngJpegTextAndImageOnlyMessagesInOrder()
    {
        var store = CreateImageStore();
        var png = await store.ImportBytesAsync(CreateImageBytes("png", 2, 3));
        var jpeg = await store.ImportBytesAsync(CreateImageBytes("jpeg", 4, 5));
        var state = State(
            User("inspect", Attachment(png), Attachment(jpeg)),
            User(null, Attachment(png)));

        var request = await new QuickChatOpenAiRequestBuilder(store)
            .BuildAsync(state, "model");
        var json = request.ToJsonString();
        var input = request["input"]!.AsArray();
        var firstContent = input[0]!["content"]!.AsArray();
        var imageOnlyContent = input[1]!["content"]!.AsArray();

        Assert.Equal(["input_text", "input_image", "input_image"],
            firstContent.Select(item => item!["type"]!.GetValue<string>()));
        Assert.StartsWith(
            "data:image/png;base64,",
            firstContent[1]!["image_url"]!.GetValue<string>());
        Assert.StartsWith(
            "data:image/jpeg;base64,",
            firstContent[2]!["image_url"]!.GetValue<string>());
        Assert.Single(imageOnlyContent);
        Assert.Equal("input_image", imageOnlyContent[0]!["type"]!.GetValue<string>());
        Assert.DoesNotContain(store.AssetsDirectory, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(png.AbsolutePath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(jpeg.AbsolutePath, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingManagedImage_ReturnsTypedFailureWithoutChangingState()
    {
        var missingName = $"{Guid.NewGuid():N}.png";
        var state = State(User(null, new QuickChatAttachment
        {
            Id = Guid.NewGuid(),
            AssetFileName = missingName,
            MediaType = "image/png"
        }));
        var before = JsonSerializer.Serialize(state);

        var exception = await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            CreateBuilder().BuildAsync(state, "model"));

        Assert.Equal(QuickChatFailureKind.MissingManagedImage, exception.FailureKind);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("C:\\outside.png")]
    public async Task UnsafePersistedImageReference_ReturnsTypedFailure(string assetFileName)
    {
        var state = State(User(null, new QuickChatAttachment
        {
            Id = Guid.NewGuid(),
            AssetFileName = assetFileName,
            MediaType = "image/png"
        }));

        var exception = await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            CreateBuilder().BuildAsync(state, "model"));

        Assert.Equal(QuickChatFailureKind.MissingManagedImage, exception.FailureKind);
    }

    [Fact]
    public async Task Success_SendsAuthenticatedStreamingResponsesRequestAndCollectsText()
    {
        string? body = null;
        string? authorization = null;
        var service = CreateService(async (request, cancellationToken) =>
        {
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            authorization = request.Headers.Authorization?.ToString();
            return SseResponse(Delta("Hello "), Delta("world"), Completed());
        }, new OpenAiTranslationConfiguration("quick-key", "quick-model"));

        var text = await service.GetResponseAsync(State(User("Hi")));

        Assert.Equal("Hello world", text);
        Assert.Equal("Bearer quick-key", authorization);
        using var document = JsonDocument.Parse(body!);
        Assert.Equal("quick-model", document.RootElement.GetProperty("model").GetString());
        Assert.True(document.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("Hi", document.RootElement
            .GetProperty("input")[0]
            .GetProperty("content")[0]
            .GetProperty("text").GetString());
    }

    [Fact]
    public async Task Streaming_EmitsDeltasInOrderThenCompletionAndIgnoresUnknownEvents()
    {
        var service = CreateService((_, _) => Task.FromResult(SseResponse(
            UnknownEvent(),
            Delta("one"),
            Delta("two"),
            Completed())));

        var events = await CollectAsync(service.StreamResponseAsync(State(User("Hi"))));

        Assert.Equal(3, events.Count);
        Assert.Equal(
            [QuickChatStreamEventKind.TextDelta,
             QuickChatStreamEventKind.TextDelta,
             QuickChatStreamEventKind.Completed],
            events.Select(item => item.Kind));
        Assert.Equal(["one", "two"], events
            .Where(item => item.Kind == QuickChatStreamEventKind.TextDelta)
            .Select(item => item.Text));
    }

    [Fact]
    public async Task MissingConfiguration_ReturnsTypedFailureWithoutSending()
    {
        var sent = false;
        var service = CreateService((_, _) =>
        {
            sent = true;
            return Task.FromResult(SseResponse(Completed()));
        }, configuration: null, useDefaultConfiguration: false);

        var exception = await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            service.GetResponseAsync(State(User("Hi"))));

        Assert.Equal(QuickChatFailureKind.NotConfigured, exception.FailureKind);
        Assert.False(sent);
    }

    [Fact]
    public async Task HttpStatusFailure_MapsToTypedFailure()
    {
        var service = CreateService((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var exception = await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            service.GetResponseAsync(State(User("Hi"))));

        Assert.Equal(QuickChatFailureKind.HttpFailure, exception.FailureKind);
        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task NetworkFailure_MapsToTypedFailure()
    {
        var service = CreateService((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));

        var exception = await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            service.GetResponseAsync(State(User("Hi"))));

        Assert.Equal(QuickChatFailureKind.HttpFailure, exception.FailureKind);
        Assert.DoesNotContain("offline", exception.Message);
    }

    [Theory]
    [InlineData("data: { malformed}\n\n")]
    [InlineData("data: {\"type\":\"response.output_text.delta\"}\n\n")]
    [InlineData("data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n\n")]
    public async Task MalformedOrIncompleteSuccessfulStream_IsRejected(string sse)
    {
        var service = CreateService((_, _) => Task.FromResult(SseResponseText(sse)));

        var exception = await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            service.GetResponseAsync(State(User("Hi"))));

        Assert.Equal(QuickChatFailureKind.InvalidResponse, exception.FailureKind);
    }

    [Fact]
    public async Task ProviderErrorEvent_MapsToHttpFailure()
    {
        var service = CreateService((_, _) => Task.FromResult(SseResponse(
            JsonSerializer.Serialize(new
            {
                type = "error",
                message = "raw provider details"
            }))));

        var exception = await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            service.GetResponseAsync(State(User("Hi"))));

        Assert.Equal(QuickChatFailureKind.HttpFailure, exception.FailureKind);
        Assert.DoesNotContain("raw provider details", exception.Message);
    }

    [Fact]
    public async Task CancellationBeforeSend_PropagatesAsCancellation()
    {
        var sent = false;
        var service = CreateService((_, _) =>
        {
            sent = true;
            return Task.FromResult(SseResponse(Completed()));
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetResponseAsync(State(User("Hi")), cancellation.Token));

        Assert.False(sent);
    }

    [Fact]
    public async Task MidStreamCancellation_StopsParsingAndPropagatesCancellation()
    {
        var firstEvent = Encoding.UTF8.GetBytes($"data: {Delta("first")}\n\n");
        var service = CreateService((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new FirstChunkThenBlockingStream(firstEvent))
            }));
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = service
            .StreamResponseAsync(State(User("Hi")), cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("first", enumerator.Current.Text);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            enumerator.MoveNextAsync().AsTask());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private QuickChatOpenAiRequestBuilder CreateBuilder() =>
        new(CreateImageStore());

    private LocalQuickChatImageStore CreateImageStore() =>
        new(Path.Combine(_directory, "quickchat-assets"));

    private OpenAiQuickChatService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        OpenAiTranslationConfiguration? configuration = null,
        bool useDefaultConfiguration = true)
    {
        if (useDefaultConfiguration && configuration is null)
        {
            configuration = new OpenAiTranslationConfiguration("test-key", "test-model");
        }

        var client = new HttpClient(new StubHttpMessageHandler(send))
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        return new OpenAiQuickChatService(
            client,
            new StubConfigurationProvider(configuration),
            CreateBuilder());
    }

    private static QuickChatConversationState State(params QuickChatMessage[] messages) =>
        new() { Messages = [.. messages] };

    private static QuickChatMessage User(
        string? text,
        params QuickChatAttachment[] attachments) =>
        new()
        {
            Id = Guid.NewGuid(),
            Role = QuickChatMessageRole.User,
            Text = text,
            Attachments = [.. attachments],
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static QuickChatMessage Assistant(
        string text,
        QuickChatMessageStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            Role = QuickChatMessageRole.Assistant,
            Text = text,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static QuickChatAttachment Attachment(ManagedQuickChatImage image) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = QuickChatAttachmentType.Image,
            AssetFileName = image.AssetFileName,
            MediaType = image.MediaType,
            Width = image.Width,
            Height = image.Height
        };

    private static HttpResponseMessage SseResponse(params string[] eventJson) =>
        SseResponseText(string.Concat(eventJson.Select(item => $"data: {item}\n\n")));

    private static HttpResponseMessage SseResponseText(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream")
        };

    private static string Delta(string text) => JsonSerializer.Serialize(new
    {
        type = "response.output_text.delta",
        delta = text
    });

    private static string Completed() => JsonSerializer.Serialize(new
    {
        type = "response.completed"
    });

    private static string UnknownEvent() => JsonSerializer.Serialize(new
    {
        type = "response.output_item.added"
    });

    private static async Task<List<QuickChatStreamEvent>> CollectAsync(
        IAsyncEnumerable<QuickChatStreamEvent> source)
    {
        var result = new List<QuickChatStreamEvent>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }

    private static byte[] CreateImageBytes(string format, int width, int height)
    {
        const int bytesPerPixel = 4;
        var pixels = new byte[width * height * bytesPerPixel];
        Array.Fill(pixels, (byte)0x80);
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * bytesPerPixel);
        BitmapEncoder encoder = format switch
        {
            "png" => new PngBitmapEncoder(),
            "jpeg" => new JpegBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private sealed class StubConfigurationProvider(
        OpenAiTranslationConfiguration? configuration)
        : IOpenAiConfigurationProvider
    {
        public OpenAiTranslationConfiguration? GetConfiguration() => configuration;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class FirstChunkThenBlockingStream(byte[] firstChunk) : Stream
    {
        private bool _sent;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!_sent)
            {
                _sent = true;
                firstChunk.AsSpan().CopyTo(buffer.Span);
                return firstChunk.Length;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
