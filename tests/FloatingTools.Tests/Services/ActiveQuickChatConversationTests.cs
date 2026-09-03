using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.Tests.Services;

public sealed class ActiveQuickChatConversationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FloatingTools.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_EmptyStoreLoadsOnceAndStartsNoRequest()
    {
        var store = new RecordingStore();
        var service = new ScriptedService();
        var session = CreateSession(store, service);

        await session.InitializeAsync();
        await session.InitializeAsync();

        Assert.True(session.IsInitialized);
        Assert.Empty(session.Messages);
        Assert.Equal(1, store.LoadCount);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Initialize_PreservesLoadedOrderAttachmentsInstructionsAndInterruptedState()
    {
        var attachment = Attachment("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png");
        var first = User("first", attachment);
        var interrupted = Assistant(
            "partial",
            QuickChatMessageStatus.Interrupted);
        var initial = State(first, interrupted);
        initial.AdditionalInstructions = "Always answer in Hebrew.";
        var session = CreateSession(new RecordingStore(initial));

        await session.InitializeAsync();

        Assert.Equal([first.Id, interrupted.Id], session.Messages.Select(item => item.Id));
        Assert.Equal(attachment.AssetFileName, session.Messages[0].Attachments[0].AssetFileName);
        Assert.Equal(QuickChatMessageStatus.Interrupted, session.Messages[1].Status);
        Assert.Equal("Always answer in Hebrew.", session.AdditionalInstructions);
    }

    [Fact]
    public async Task Initialize_UsesJsonStoreNormalizationForPersistedInProgressResponse()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, JsonQuickChatStore.StorageFileName);
        await File.WriteAllTextAsync(path,
            """
            {
              "schemaVersion": 1,
              "messages": [
                {
                  "id": "10000000-0000-0000-0000-000000000001",
                  "role": "assistant",
                  "text": "crash partial",
                  "attachments": [],
                  "createdAt": "2026-01-01T00:00:00Z",
                  "status": "inProgress"
                }
              ]
            }
            """);
        var service = new ScriptedService();
        var session = new ActiveQuickChatConversation(
            new JsonQuickChatStore(path),
            service,
            new RecordingImageStore(_directory));

        await session.InitializeAsync();

        Assert.Equal(QuickChatMessageStatus.Interrupted, Assert.Single(session.Messages).Status);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Send_RejectsMessageWithoutTextOrAttachments()
    {
        var session = CreateSession();
        await session.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => session.SendAsync("   "));

        Assert.Empty(session.Messages);
    }

    [Fact]
    public async Task TextSend_PersistsUserThenOneInProgressAssistantAndCompletes()
    {
        var store = new RecordingStore();
        var service = new ScriptedService(CompletedScript("answer"));
        var session = CreateSession(store, service);
        await session.InitializeAsync();

        await session.SendAsync("question");

        Assert.Equal(2, session.Messages.Count);
        Assert.Equal(QuickChatMessageRole.User, session.Messages[0].Role);
        Assert.Equal("question", session.Messages[0].Text);
        var assistant = session.Messages[1];
        Assert.Equal(QuickChatMessageRole.Assistant, assistant.Role);
        Assert.Equal("answer", assistant.Text);
        Assert.Equal(QuickChatMessageStatus.Completed, assistant.Status);
        // The user message and the assistant placeholder are persisted
        // together, in one save, before generation starts — both durably
        // saved before HTTP begins, same as before, in a single write.
        Assert.Contains(store.Saves, state =>
            state.Messages.Count == 2
            && state.Messages[0].Text == "question"
            && state.Messages[1].Status == QuickChatMessageStatus.InProgress);
        Assert.Equal(QuickChatMessageStatus.Completed, store.Saves[^1].Messages[1].Status);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task ImageOnlyAndTextImageSendsPersistPreparedAttachments()
    {
        var attachment = Attachment("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png");
        var service = new ScriptedService(
            CompletedScript("first"),
            CompletedScript("second"));
        var session = CreateSession(openAiService: service);
        await session.InitializeAsync();

        await session.SendAsync(null, [attachment]);
        await session.SendAsync("inspect", [attachment]);

        Assert.Equal(4, session.Messages.Count);
        Assert.Null(session.Messages[0].Text);
        Assert.Equal(attachment.AssetFileName, session.Messages[0].Attachments[0].AssetFileName);
        Assert.Equal("inspect", session.Messages[2].Text);
        Assert.Equal(attachment.AssetFileName, session.Messages[2].Attachments[0].AssetFileName);
        Assert.NotSame(attachment, session.Messages[0].Attachments[0]);
    }

    [Fact]
    public async Task DeleteUnsentAttachment_RemovesItFromTheAttachmentContentCache()
    {
        var attachment = Attachment("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png");
        var cache = new QuickChatAttachmentContentCache();
        cache.SetDataUrl(attachment.AssetFileName, "data:image/png;base64,cached");
        var session = CreateSession(attachmentContentCache: cache);
        await session.InitializeAsync();

        await session.DeleteUnsentAttachmentAsync(attachment.AssetFileName);

        Assert.False(cache.TryGetDataUrl(attachment.AssetFileName, out _));
    }

    [Fact]
    public async Task NewChat_RemovesEveryPreviousAttachmentFromTheAttachmentContentCache()
    {
        var attachment = Attachment("cccccccccccccccccccccccccccccc.png");
        var imageStore = new RecordingImageStore(_directory, attachment.AssetFileName);
        var cache = new QuickChatAttachmentContentCache();
        cache.SetDataUrl(attachment.AssetFileName, "data:image/png;base64,cached");
        var store = new RecordingStore(State(User("caption", attachment)));
        var session = CreateSession(
            store,
            imageStore: imageStore,
            attachmentContentCache: cache);
        await session.InitializeAsync();

        await session.NewChatAsync();

        Assert.False(cache.TryGetDataUrl(attachment.AssetFileName, out _));
    }

    [Fact]
    public async Task DeltasAccumulateInOrderAndTerminalContentPersistsImmediately()
    {
        var store = new RecordingStore();
        var session = CreateSession(
            store,
            new ScriptedService(CompletedScript("one", " two", " three")),
            partialSaveInterval: TimeSpan.FromHours(1),
            timeProvider: new FixedTimeProvider());
        await session.InitializeAsync();

        await session.SendAsync("question");

        var assistant = session.Messages[1];
        Assert.Equal("one two three", assistant.Text);
        Assert.Equal(QuickChatMessageStatus.Completed, assistant.Status);
        var partialSaves = store.Saves.Count(state =>
            state.Messages.Count == 2
            && state.Messages[1].Status == QuickChatMessageStatus.InProgress
            && !string.IsNullOrEmpty(state.Messages[1].Text));
        Assert.Equal(1, partialSaves);
        Assert.Equal("one two three", store.Saves[^1].Messages[1].Text);
        Assert.Equal(QuickChatMessageStatus.Completed, store.Saves[^1].Messages[1].Status);
    }

    [Fact]
    public async Task Stop_PreservesPartialTextMarksInterruptedAndIsRepeatable()
    {
        var blocking = new BlockingScript("partial");
        var session = CreateSession(openAiService: new ScriptedService(blocking.Run));
        await session.InitializeAsync();
        var send = session.SendAsync("question");
        await blocking.DeltaEmitted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await session.StopAsync();
        await session.StopAsync();
        await send;

        var assistant = session.Messages[1];
        Assert.Equal("partial", assistant.Text);
        Assert.Equal(QuickChatMessageStatus.Interrupted, assistant.Status);
        Assert.Null(assistant.ErrorMessage);
        Assert.False(session.IsGenerating);
    }

    [Fact]
    public async Task ProviderFailure_PreservesUserAndPartialTextAsSafePersistedError()
    {
        var store = new RecordingStore();
        var service = new ScriptedService(FailingScript(
            "partial",
            new QuickChatServiceException(
                QuickChatFailureKind.HttpFailure,
                "raw provider detail")));
        var session = CreateSession(store, service);
        await session.InitializeAsync();

        await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            session.SendAsync("question"));

        Assert.Equal("question", session.Messages[0].Text);
        var assistant = session.Messages[1];
        Assert.Equal("partial", assistant.Text);
        Assert.Equal(QuickChatMessageStatus.Error, assistant.Status);
        Assert.Equal("Quick Chat could not reach the AI service.", assistant.ErrorMessage);
        Assert.DoesNotContain("raw provider detail", assistant.ErrorMessage);
        Assert.Equal(QuickChatMessageStatus.Error, store.Saves[^1].Messages[1].Status);
        Assert.Equal(assistant.ErrorMessage, store.Saves[^1].Messages[1].ErrorMessage);
    }

    [Fact]
    public async Task UnexpectedFailure_IsPersistedSafelyAndNotSwallowed()
    {
        var session = CreateSession(openAiService: new ScriptedService(
            FailingScript(null, new InvalidOperationException("internal secret"))));
        await session.InitializeAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.SendAsync("question"));

        Assert.Equal("internal secret", exception.Message);
        Assert.Equal(QuickChatMessageStatus.Error, session.Messages[1].Status);
        Assert.Equal(
            "Quick Chat could not complete the request.",
            session.Messages[1].ErrorMessage);
    }

    [Fact]
    public async Task Retry_ReusesFailedAssistantUserAndAttachmentsWithoutDuplicates()
    {
        var attachment = Attachment("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png");
        var user = User("question", attachment);
        var failed = Assistant("old partial", QuickChatMessageStatus.Error);
        failed.ErrorMessage = "old error";
        var service = new ScriptedService(CompletedScript("retry answer"));
        var session = CreateSession(
            new RecordingStore(State(user, failed)),
            service);
        await session.InitializeAsync();

        await session.RetryAsync(failed.Id);

        Assert.Equal(2, session.Messages.Count);
        Assert.Equal(user.Id, session.Messages[0].Id);
        Assert.Equal(failed.Id, session.Messages[1].Id);
        Assert.Equal(attachment.AssetFileName, session.Messages[0].Attachments[0].AssetFileName);
        Assert.Equal("retry answer", session.Messages[1].Text);
        Assert.Equal(QuickChatMessageStatus.Completed, session.Messages[1].Status);
        Assert.Null(session.Messages[1].ErrorMessage);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task FailedRetry_ReusesAssistantAndRemainsError()
    {
        var user = User("question");
        var failed = Assistant("old", QuickChatMessageStatus.Error);
        var session = CreateSession(
            new RecordingStore(State(user, failed)),
            new ScriptedService(FailingScript(
                "new partial",
                new QuickChatServiceException(
                    QuickChatFailureKind.InvalidResponse,
                    "raw"))));
        await session.InitializeAsync();

        await Assert.ThrowsAsync<QuickChatServiceException>(() =>
            session.RetryAsync(failed.Id));

        Assert.Equal(2, session.Messages.Count);
        Assert.Equal(failed.Id, session.Messages[1].Id);
        Assert.Equal("new partial", session.Messages[1].Text);
        Assert.Equal(QuickChatMessageStatus.Error, session.Messages[1].Status);
        Assert.Equal("Quick Chat received an invalid response.", session.Messages[1].ErrorMessage);
    }

    [Theory]
    [InlineData(QuickChatMessageStatus.Completed)]
    [InlineData(QuickChatMessageStatus.Interrupted)]
    [InlineData(QuickChatMessageStatus.InProgress)]
    public async Task Retry_RejectsNonErrorAssistant(QuickChatMessageStatus status)
    {
        var assistant = Assistant("answer", status);
        var service = new ScriptedService();
        var session = CreateSession(
            new RecordingStore(State(User("question"), assistant)),
            service);
        await session.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RetryAsync(assistant.Id));

        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task AdditionalInstructions_PersistWithoutChangingMessagesOrGenerating()
    {
        var user = User("existing");
        var store = new RecordingStore(State(user));
        var service = new ScriptedService();
        var session = CreateSession(store, service);
        await session.InitializeAsync();

        await session.SetAdditionalInstructionsAsync("Always answer in Hebrew.");

        Assert.Equal("Always answer in Hebrew.", session.AdditionalInstructions);
        Assert.Single(session.Messages);
        Assert.Equal(user.Id, session.Messages[0].Id);
        Assert.Equal(0, service.CallCount);
        Assert.Equal("Always answer in Hebrew.", store.Saves[^1].AdditionalInstructions);
        Assert.DoesNotContain(
            QuickChatOpenAiRequestBuilder.ProductInstruction,
            JsonSerializer.Serialize(store.Saves[^1]));
    }

    [Fact]
    public async Task StartupSweep_PreservesReferencedDeletesOrphanAndLeavesNotesAssets()
    {
        var quickAssets = new LocalQuickChatImageStore(
            Path.Combine(_directory, "quickchat-assets"));
        var referenced = await quickAssets.ImportBytesAsync(CreatePngBytes());
        var orphan = await quickAssets.ImportBytesAsync(CreatePngBytes());
        var notesDirectory = Path.Combine(_directory, "notes-assets");
        Directory.CreateDirectory(notesDirectory);
        var notesSentinel = Path.Combine(notesDirectory, "note.png");
        await File.WriteAllTextAsync(notesSentinel, "notes");
        var state = State(User("saved", Attachment(referenced)));
        var session = new ActiveQuickChatConversation(
            new RecordingStore(state),
            new ScriptedService(),
            quickAssets);

        await session.InitializeAsync();

        Assert.True(File.Exists(referenced.AbsolutePath));
        Assert.False(File.Exists(orphan.AbsolutePath));
        Assert.Equal("notes", await File.ReadAllTextAsync(notesSentinel));
    }

    [Fact]
    public async Task NewChat_PreservesInstructionsClearsMessagesPersistsThenDeletesAssets()
    {
        var quickAssets = new LocalQuickChatImageStore(
            Path.Combine(_directory, "quickchat-assets"));
        var image = await quickAssets.ImportBytesAsync(CreatePngBytes());
        var state = State(User("saved", Attachment(image)));
        state.AdditionalInstructions = "Keep this setting";
        var store = new RecordingStore(state);
        var session = new ActiveQuickChatConversation(
            store,
            new ScriptedService(),
            quickAssets);
        await session.InitializeAsync();

        await session.NewChatAsync();

        Assert.Empty(session.Messages);
        Assert.Equal("Keep this setting", session.AdditionalInstructions);
        Assert.Empty(store.Saves[^1].Messages);
        Assert.Equal("Keep this setting", store.Saves[^1].AdditionalInstructions);
        Assert.False(File.Exists(image.AbsolutePath));
    }

    [Fact]
    public async Task NewChatDuringGeneration_CancelsAndLeavesEmptyPersistedState()
    {
        var blocking = new BlockingScript("partial");
        var store = new RecordingStore();
        var session = CreateSession(
            store,
            new ScriptedService(blocking.Run));
        await session.InitializeAsync();
        var send = session.SendAsync("question");
        await blocking.DeltaEmitted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await session.NewChatAsync();
        await send;

        Assert.Empty(session.Messages);
        Assert.Empty(store.Saves[^1].Messages);
        Assert.False(session.IsGenerating);
    }

    [Fact]
    public async Task PrepareForExit_CancelsGenerationPersistsInterruptedAndKeepsAssets()
    {
        var blocking = new BlockingScript("partial");
        var imageStore = new RecordingImageStore(_directory);
        var store = new RecordingStore();
        var session = CreateSession(
            store,
            new ScriptedService(blocking.Run),
            imageStore);
        await session.InitializeAsync();
        imageStore.AddManaged("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png");
        var send = session.SendAsync(
            "question",
            [Attachment("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png")]);
        await blocking.DeltaEmitted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await session.PrepareForExitAsync();
        await send;

        Assert.Equal(QuickChatMessageStatus.Interrupted, session.Messages[1].Status);
        Assert.Equal("partial", store.Saves[^1].Messages[1].Text);
        Assert.Equal(QuickChatMessageStatus.Interrupted, store.Saves[^1].Messages[1].Status);
        Assert.Empty(imageStore.Deleted);
    }

    [Fact]
    public async Task DeleteUnsentAttachment_DeletesOnlyUnreferencedManagedAsset()
    {
        var referenced = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png";
        var unsent = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg";
        var imageStore = new RecordingImageStore(_directory, referenced, unsent);
        var session = CreateSession(
            new RecordingStore(State(User("saved", Attachment(referenced)))),
            imageStore: imageStore);
        await session.InitializeAsync();

        await session.DeleteUnsentAttachmentAsync(unsent);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.DeleteUnsentAttachmentAsync(referenced));

        Assert.Contains(unsent, imageStore.Deleted);
        Assert.DoesNotContain(referenced, imageStore.Deleted);
    }

    [Fact]
    public async Task ConcurrentSendAndRetryAreRejectedWhileGenerating()
    {
        var blocking = new BlockingScript("partial");
        var failed = Assistant("failed", QuickChatMessageStatus.Error);
        var initial = State(User("old question"), failed);
        var service = new ScriptedService(blocking.Run);
        var session = CreateSession(new RecordingStore(initial), service);
        await session.InitializeAsync();
        var send = session.SendAsync("new question");
        await blocking.DeltaEmitted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.SendAsync("parallel"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RetryAsync(failed.Id));
        await session.StopAsync();
        await send;

        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task ChangeEvent_ReportsMessageStreamingGenerationAndInstructions()
    {
        var session = CreateSession(openAiService: new ScriptedService(
            CompletedScript("answer")));
        var changes = new List<QuickChatConversationChangeKind>();
        session.Changed += (_, args) => changes.Add(args.Kind);

        await session.InitializeAsync();
        await session.SetAdditionalInstructionsAsync("instruction");
        await session.SendAsync("question");

        Assert.Contains(QuickChatConversationChangeKind.Reset, changes);
        Assert.Contains(QuickChatConversationChangeKind.AdditionalInstructionsChanged, changes);
        Assert.Equal(2, changes.Count(kind =>
            kind == QuickChatConversationChangeKind.MessageAdded));
        Assert.Contains(QuickChatConversationChangeKind.MessageUpdated, changes);
        Assert.Equal(2, changes.Count(kind =>
            kind == QuickChatConversationChangeKind.IsGeneratingChanged));
    }

    private ActiveQuickChatConversation CreateSession(
        RecordingStore? store = null,
        ScriptedService? openAiService = null,
        IQuickChatImageStore? imageStore = null,
        TimeSpan? partialSaveInterval = null,
        TimeProvider? timeProvider = null,
        QuickChatAttachmentContentCache? attachmentContentCache = null) =>
        new(
            store ?? new RecordingStore(),
            openAiService ?? new ScriptedService(),
            imageStore ?? new RecordingImageStore(_directory),
            partialSaveInterval,
            timeProvider,
            attachmentContentCache);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
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

    private static QuickChatAttachment Attachment(string assetFileName) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = QuickChatAttachmentType.Image,
            AssetFileName = assetFileName,
            MediaType = Path.GetExtension(assetFileName) == ".jpg"
                ? "image/jpeg"
                : "image/png",
            Width = 1,
            Height = 1
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

    private static Func<CancellationToken, IAsyncEnumerable<QuickChatStreamEvent>>
        CompletedScript(params string[] deltas) =>
        cancellationToken => Events(deltas, cancellationToken);

    private static async IAsyncEnumerable<QuickChatStreamEvent> Events(
        IEnumerable<string> deltas,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var delta in deltas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new QuickChatStreamEvent(
                QuickChatStreamEventKind.TextDelta,
                delta);
            await Task.Yield();
        }

        yield return new QuickChatStreamEvent(QuickChatStreamEventKind.Completed);
    }

    private static Func<CancellationToken, IAsyncEnumerable<QuickChatStreamEvent>>
        FailingScript(string? partial, Exception exception) =>
        cancellationToken => FailAfterPartial(partial, exception, cancellationToken);

    private static async IAsyncEnumerable<QuickChatStreamEvent> FailAfterPartial(
        string? partial,
        Exception exception,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (partial is not null)
        {
            yield return new QuickChatStreamEvent(
                QuickChatStreamEventKind.TextDelta,
                partial);
            await Task.Yield();
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw exception;
    }

    private static byte[] CreatePngBytes()
    {
        var pixels = new byte[] { 0x80, 0x80, 0x80, 0xFF };
        var bitmap = BitmapSource.Create(
            1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static QuickChatConversationState CloneState(
        QuickChatConversationState state) =>
        new()
        {
            SchemaVersion = state.SchemaVersion,
            AdditionalInstructions = state.AdditionalInstructions,
            Messages = state.Messages.Select(message => new QuickChatMessage
            {
                Id = message.Id,
                Role = message.Role,
                Text = message.Text,
                Attachments = message.Attachments.Select(attachment => new QuickChatAttachment
                {
                    Id = attachment.Id,
                    Type = attachment.Type,
                    AssetFileName = attachment.AssetFileName,
                    MediaType = attachment.MediaType,
                    Width = attachment.Width,
                    Height = attachment.Height
                }).ToList(),
                CreatedAt = message.CreatedAt,
                Status = message.Status,
                ErrorMessage = message.ErrorMessage
            }).ToList()
        };

    private sealed class RecordingStore(QuickChatConversationState? initial = null)
        : IQuickChatStore
    {
        private readonly QuickChatConversationState _initial =
            CloneState(initial ?? new QuickChatConversationState());

        public int LoadCount { get; private set; }

        public List<QuickChatConversationState> Saves { get; } = [];

        public Task<QuickChatConversationState> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCount++;
            return Task.FromResult(CloneState(_initial));
        }

        public Task SaveAsync(
            QuickChatConversationState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Saves.Add(CloneState(state));
            return Task.CompletedTask;
        }
    }

    private sealed class ScriptedService(
        params Func<CancellationToken, IAsyncEnumerable<QuickChatStreamEvent>>[] scripts)
        : IQuickChatOpenAiService
    {
        private readonly Queue<Func<CancellationToken, IAsyncEnumerable<QuickChatStreamEvent>>>
            _scripts = new(scripts);

        public int CallCount { get; private set; }

        public IAsyncEnumerable<QuickChatStreamEvent> StreamResponseAsync(
            QuickChatConversationState conversation,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_scripts.Count == 0)
            {
                throw new InvalidOperationException("No response script was configured.");
            }

            return _scripts.Dequeue()(cancellationToken);
        }

        public async Task<string> GetResponseAsync(
            QuickChatConversationState conversation,
            CancellationToken cancellationToken = default)
        {
            var text = new StringBuilder();
            await foreach (var item in StreamResponseAsync(conversation, cancellationToken))
            {
                if (item.Kind == QuickChatStreamEventKind.TextDelta)
                {
                    text.Append(item.Text);
                }
            }

            return text.ToString();
        }
    }

    private sealed class BlockingScript(string partial)
    {
        public TaskCompletionSource DeltaEmitted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<QuickChatStreamEvent> Run(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new QuickChatStreamEvent(
                QuickChatStreamEventKind.TextDelta,
                partial);
            DeltaEmitted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class RecordingImageStore(
        string directory,
        params string[] managedFileNames) : IQuickChatImageStore
    {
        private readonly HashSet<string> _managed =
            new(managedFileNames, StringComparer.OrdinalIgnoreCase);

        public List<string> Deleted { get; } = [];

        public void AddManaged(string assetFileName) => _managed.Add(assetFileName);

        public Task<ManagedQuickChatImage> ImportFileAsync(
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ManagedQuickChatImage> ImportBytesAsync(
            ReadOnlyMemory<byte> imageBytes,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string GetAbsolutePath(string assetFileName) =>
            Path.Combine(directory, assetFileName);

        public IReadOnlyList<string> GetManagedAssetFileNames() =>
            _managed.Order(StringComparer.Ordinal).ToArray();

        public Task DeleteAsync(
            string assetFileName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Deleted.Add(assetFileName);
            _managed.Remove(assetFileName);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
