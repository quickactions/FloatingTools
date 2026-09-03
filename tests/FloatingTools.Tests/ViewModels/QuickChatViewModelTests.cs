using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

public sealed class QuickChatViewModelTests
{
    [Fact]
    public void PageNavigation_StartsOnChatAndRoundTripsThroughSettings()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(QuickChatPage.Chat, viewModel.CurrentPage);
        Assert.True(viewModel.IsChatPage);
        Assert.False(viewModel.IsSettingsPage);

        viewModel.IsHeaderExpanded = true;
        viewModel.OpenSettingsCommand.Execute(null);

        Assert.Equal(QuickChatPage.Settings, viewModel.CurrentPage);
        Assert.False(viewModel.IsHeaderExpanded);
        Assert.False(viewModel.IsChatPage);
        Assert.True(viewModel.IsSettingsPage);

        viewModel.BackToChatCommand.Execute(null);

        Assert.Equal(QuickChatPage.Chat, viewModel.CurrentPage);
        Assert.True(viewModel.IsChatPage);
        Assert.False(viewModel.IsSettingsPage);
    }

    [Fact]
    public async Task Initialize_ProjectsExistingConversationWithoutGenerationOrDuplication()
    {
        var user = User("question");
        var assistant = Assistant("answer", QuickChatMessageStatus.Completed);
        var session = new FakeSession(State(user, assistant));
        var viewModel = CreateViewModel(session);

        await viewModel.InitializeAsync();
        var firstProjection = viewModel.Messages[0];
        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.Messages.Count);
        Assert.Equal([user.Id, assistant.Id], viewModel.Messages.Select(item => item.Id));
        Assert.Same(firstProjection, viewModel.Messages[0]);
        Assert.Equal(2, session.InitializeCount);
        Assert.Equal(0, session.SendCount);
        Assert.Equal(0, session.RetryCount);
    }

    [Fact]
    public async Task MessageAdded_AddsOneStablePresentationMessageWithAttachments()
    {
        var session = new FakeSession();
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();
        var message = User("image", Attachment("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png"));

        session.AddMessage(message);
        session.RaiseMessageAdded(message);

        var presentation = Assert.Single(viewModel.Messages);
        Assert.Equal(message.Id, presentation.Id);
        Assert.True(presentation.IsUser);
        Assert.Equal("image", presentation.Text);
        Assert.True(presentation.HasAttachments);
        Assert.Equal(
            message.Attachments[0].AssetFileName,
            presentation.Attachments[0].AssetFileName);
        Assert.Equal(
            $"C:\\managed\\{message.Attachments[0].AssetFileName}",
            presentation.Attachments[0].RuntimePath);
    }

    [Fact]
    public async Task MessageUpdated_UpdatesOnlyMatchingStablePresentationObject()
    {
        var first = User("first");
        var second = Assistant("partial", QuickChatMessageStatus.InProgress);
        var session = new FakeSession(State(first, second));
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();
        var firstPresentation = viewModel.Messages[0];
        var secondPresentation = viewModel.Messages[1];
        var propertyChanges = new List<string?>();
        secondPresentation.PropertyChanged += (_, args) =>
            propertyChanges.Add(args.PropertyName);

        second.Text = "partial completed";
        second.Status = QuickChatMessageStatus.Completed;
        session.RaiseMessageUpdated(second);

        Assert.Same(firstPresentation, viewModel.Messages[0]);
        Assert.Same(secondPresentation, viewModel.Messages[1]);
        Assert.Equal("partial completed", secondPresentation.Text);
        Assert.True(secondPresentation.IsCompleted);
        Assert.False(secondPresentation.IsInProgress);
        Assert.Contains(nameof(QuickChatMessageViewModel.Text), propertyChanges);
        Assert.Contains(nameof(QuickChatMessageViewModel.Status), propertyChanges);
    }

    [Fact]
    public async Task IsGeneratingAndScrollFollowSignalsReflectSessionChanges()
    {
        var session = new FakeSession();
        var viewModel = CreateViewModel(session);
        var scrollSignals = 0;
        viewModel.ScrollFollowRequested += (_, _) => scrollSignals++;
        await viewModel.InitializeAsync();

        session.SetGenerating(true);
        var message = Assistant("one", QuickChatMessageStatus.InProgress);
        session.AddMessage(message);
        session.RaiseMessageAdded(message);
        message.Text = "one two";
        session.RaiseMessageUpdated(message);
        session.SetGenerating(false);

        Assert.False(viewModel.IsGenerating);
        Assert.Equal(2, scrollSignals);
        Assert.Equal("one two", viewModel.Messages[0].Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespaceComposerCannotSend(string? draft)
    {
        var session = new FakeSession();
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();

        viewModel.DraftText = draft ?? string.Empty;

        Assert.False(viewModel.CanSend);
        Assert.False(viewModel.SendCommand.CanExecute(null));
        await viewModel.SendAsync();
        Assert.Equal(0, session.SendCount);
    }

    [Fact]
    public async Task TextSend_TransfersAcceptedDraftAndClearsIt()
    {
        var session = new FakeSession();
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();
        viewModel.DraftText = "question";

        await viewModel.SendAsync();

        Assert.Equal(1, session.SendCount);
        Assert.Equal("question", session.LastSendText);
        Assert.Empty(session.LastSendAttachments);
        Assert.Equal(string.Empty, viewModel.DraftText);
        Assert.Empty(viewModel.PendingAttachments);
    }

    [Fact]
    public async Task ImageOnlyAndTextImageSendUsePreparedPendingAttachments()
    {
        var session = new FakeSession();
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"),
            Managed("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg", "image/jpeg"));
        var viewModel = CreateViewModel(session, images);
        await viewModel.InitializeAsync();
        await viewModel.AttachImageFileAsync("first.png");

        Assert.True(viewModel.CanSend);
        await viewModel.SendAsync();
        Assert.True(string.IsNullOrEmpty(session.SendHistory[0].Text));
        Assert.Single(session.SendHistory[0].Attachments);

        await viewModel.AttachImageFileAsync("second.jpg");
        viewModel.DraftText = "inspect";
        await viewModel.SendAsync();

        Assert.Equal("inspect", session.SendHistory[1].Text);
        Assert.Single(session.SendHistory[1].Attachments);
        Assert.Empty(viewModel.PendingAttachments);
    }

    [Fact]
    public async Task RejectedSendKeepsDraftAndPendingOwnership()
    {
        var session = new FakeSession { RejectSend = true };
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"));
        var viewModel = CreateViewModel(session, images);
        await viewModel.InitializeAsync();
        viewModel.DraftText = "question";
        await viewModel.AttachImageFileAsync("image.png");

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.SendAsync());

        Assert.Equal("question", viewModel.DraftText);
        Assert.Single(viewModel.PendingAttachments);
        Assert.Empty(session.DeletedUnsentAssets);
    }

    [Fact]
    public async Task FileImportCreatesPendingManagedAttachment()
    {
        var managed = Managed(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png",
            "image/png",
            640,
            480);
        var images = new FakeImageStore(managed);
        var viewModel = CreateViewModel(imageStore: images);
        await viewModel.InitializeAsync();

        var added = await viewModel.AttachImageFileAsync("C:\\external\\photo.jpg");

        Assert.True(added);
        Assert.Equal("C:\\external\\photo.jpg", images.ImportedFilePath);
        var pending = Assert.Single(viewModel.PendingAttachments);
        Assert.Equal(managed.AssetFileName, pending.AssetFileName);
        Assert.Equal("image/png", pending.MediaType);
        Assert.Equal(640, pending.Width);
        Assert.Equal(480, pending.Height);
        Assert.DoesNotContain("external", pending.AssetFileName);
    }

    [Fact]
    public async Task ClipboardImageImportUsesDedicatedProviderAndCreatesPendingAsset()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var clipboardImages = new FakeClipboardImageProvider(bytes);
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"));
        var viewModel = CreateViewModel(
            imageStore: images,
            clipboardImageProvider: clipboardImages);
        await viewModel.InitializeAsync();

        var added = await viewModel.AttachClipboardImageAsync();

        Assert.True(added);
        Assert.Equal(bytes, images.ImportedBytes);
        Assert.Single(viewModel.PendingAttachments);
    }

    [Fact]
    public async Task PendingAttachments_AcceptThreeAndRejectFourthBeforeFileImport()
    {
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"),
            Managed("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png", "image/png"),
            Managed("cccccccccccccccccccccccccccccccc.png", "image/png"));
        var viewModel = CreateViewModel(imageStore: images);
        await viewModel.InitializeAsync();

        for (var index = 0; index < QuickChatViewModel.MaximumPendingAttachmentCount; index++)
        {
            Assert.True(await viewModel.AttachImageFileAsync($"image-{index}.png"));
        }

        var fourthAdded = await viewModel.AttachImageFileAsync("image-4.png");

        Assert.False(fourthAdded);
        Assert.Equal(3, viewModel.PendingAttachments.Count);
        Assert.False(viewModel.CanAttachImage);
        Assert.False(viewModel.AttachClipboardImageCommand.CanExecute(null));
        Assert.Equal(3, images.ImportFileCount);
    }

    [Fact]
    public async Task FourthClipboardAttachmentIsRejectedBeforeClipboardOrManagedImport()
    {
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"),
            Managed("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png", "image/png"),
            Managed("cccccccccccccccccccccccccccccccc.png", "image/png"));
        var clipboard = new FakeClipboardImageProvider([1, 2, 3]);
        var viewModel = CreateViewModel(
            imageStore: images,
            clipboardImageProvider: clipboard);
        await viewModel.InitializeAsync();
        for (var index = 0; index < QuickChatViewModel.MaximumPendingAttachmentCount; index++)
        {
            Assert.True(await viewModel.AttachImageFileAsync($"image-{index}.png"));
        }

        var fourthAdded = await viewModel.AttachClipboardImageAsync();

        Assert.False(fourthAdded);
        Assert.Equal(3, viewModel.PendingAttachments.Count);
        Assert.Equal(0, clipboard.ReadCount);
        Assert.Null(images.ImportedBytes);
    }

    [Fact]
    public async Task ParallelCapacityRaceDeletesOnlyTheImportedAttachmentThatCannotBeAdded()
    {
        var session = new FakeSession();
        var images = new CapacityRaceImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"),
            Managed("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png", "image/png"),
            Managed("cccccccccccccccccccccccccccccccc.png", "image/png"),
            Managed("dddddddddddddddddddddddddddddddd.png", "image/png"));
        var viewModel = CreateViewModel(session, imageStore: images);
        await viewModel.InitializeAsync();
        Assert.True(await viewModel.AttachImageFileAsync("first.png"));
        Assert.True(await viewModel.AttachImageFileAsync("second.png"));

        var results = await Task.WhenAll(
            viewModel.AttachImageFileAsync("third.png"),
            viewModel.AttachImageFileAsync("fourth.png"));

        Assert.Equal([false, true], results.Order());
        Assert.Equal(3, viewModel.PendingAttachments.Count);
        var deleted = Assert.Single(session.DeletedUnsentAssets);
        Assert.Contains(deleted, new[]
        {
            "cccccccccccccccccccccccccccccccc.png",
            "dddddddddddddddddddddddddddddddd.png"
        });
        Assert.DoesNotContain(viewModel.PendingAttachments,
            pending => pending.AssetFileName == deleted);
    }

    [Fact]
    public async Task EmptyClipboardImageIsSafeNoOp()
    {
        var viewModel = CreateViewModel(
            clipboardImageProvider: new FakeClipboardImageProvider(null));
        await viewModel.InitializeAsync();

        var added = await viewModel.AttachClipboardImageAsync();

        Assert.False(added);
        Assert.Empty(viewModel.PendingAttachments);
    }

    [Fact]
    public async Task RemovePendingAttachmentDeletesOnlyItsManagedAsset()
    {
        var session = new FakeSession();
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"),
            Managed("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg", "image/jpeg"));
        var viewModel = CreateViewModel(session, images);
        await viewModel.InitializeAsync();
        await viewModel.AttachImageFileAsync("first.png");
        await viewModel.AttachImageFileAsync("second.jpg");
        var removed = viewModel.PendingAttachments[0];
        var kept = viewModel.PendingAttachments[1];

        await viewModel.RemovePendingAttachmentAsync(removed);

        Assert.Equal([removed.AssetFileName], session.DeletedUnsentAssets);
        Assert.Equal(kept, Assert.Single(viewModel.PendingAttachments));
    }

    [Fact]
    public async Task DisposeCleansUnsentAssetsButNeverTransferredAssets()
    {
        var session = new FakeSession();
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"),
            Managed("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg", "image/jpeg"));
        var viewModel = CreateViewModel(session, images);
        await viewModel.InitializeAsync();
        await viewModel.AttachImageFileAsync("sent.png");
        var sentName = viewModel.PendingAttachments[0].AssetFileName;
        await viewModel.SendAsync();
        await viewModel.AttachImageFileAsync("unsent.jpg");
        var unsentName = viewModel.PendingAttachments[0].AssetFileName;

        await viewModel.DisposeAsync();

        Assert.DoesNotContain(sentName, session.DeletedUnsentAssets);
        Assert.Contains(unsentName, session.DeletedUnsentAssets);
        Assert.Empty(viewModel.PendingAttachments);
        Assert.Equal(0, session.SubscriberCount);
    }

    [Fact]
    public async Task StopDelegatesWithoutCreatingViewModelCancellationSource()
    {
        var session = new FakeSession();
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();
        session.SetGenerating(true);

        await viewModel.StopAsync();

        Assert.Equal(1, session.StopCount);
    }

    [Theory]
    [InlineData(QuickChatMessageStatus.Error, true)]
    [InlineData(QuickChatMessageStatus.Completed, false)]
    [InlineData(QuickChatMessageStatus.Interrupted, false)]
    [InlineData(QuickChatMessageStatus.InProgress, false)]
    public void MessagePresentationExposesApprovedRetryPolicy(
        QuickChatMessageStatus status,
        bool expected)
    {
        var presentation = new QuickChatMessageViewModel(Assistant("text", status));

        Assert.Equal(expected, presentation.CanRetry);
        Assert.Equal(status == QuickChatMessageStatus.Error, presentation.IsError);
        Assert.Equal(status == QuickChatMessageStatus.Interrupted, presentation.IsInterrupted);
    }

    [Fact]
    public async Task RetryDelegatesUsingCorrectAssistantId()
    {
        var failed = Assistant("partial", QuickChatMessageStatus.Error);
        var session = new FakeSession(State(User("question"), failed));
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();

        await viewModel.RetryAsync(viewModel.Messages[1]);

        Assert.Equal(1, session.RetryCount);
        Assert.Equal(failed.Id, session.LastRetryId);
    }

    [Fact]
    public async Task NewChatDelegatesClearsComposerPendingAndMessagesButKeepsInstructions()
    {
        var state = State(User("old"));
        state.AdditionalInstructions = "Keep this";
        var session = new FakeSession(state);
        var images = new FakeImageStore(
            Managed("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png", "image/png"));
        var viewModel = CreateViewModel(session, images);
        await viewModel.InitializeAsync();
        viewModel.DraftText = "draft";
        await viewModel.AttachImageFileAsync("pending.png");
        var pendingName = viewModel.PendingAttachments[0].AssetFileName;

        await viewModel.NewChatAsync();

        Assert.Equal(1, session.NewChatCount);
        Assert.Empty(viewModel.Messages);
        Assert.Equal(string.Empty, viewModel.DraftText);
        Assert.Empty(viewModel.PendingAttachments);
        Assert.Contains(pendingName, session.DeletedUnsentAssets);
        Assert.Equal("Keep this", viewModel.AdditionalInstructions);
    }

    [Fact]
    public async Task NewChatCommand_RequestsConfirmationWhenConversationOrComposerHasContent()
    {
        var session = new FakeSession(State(User("old")));
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();

        await viewModel.NewChatCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsNewChatConfirmationOpen);
        Assert.Equal(0, session.NewChatCount);

        await viewModel.ConfirmNewChatCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsNewChatConfirmationOpen);
        Assert.Equal(1, session.NewChatCount);
    }

    [Fact]
    public async Task NewChatCommand_ExecutesDirectlyWhenEverythingIsEmpty()
    {
        var session = new FakeSession();
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();

        await viewModel.NewChatCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsNewChatConfirmationOpen);
        Assert.Equal(1, session.NewChatCount);
    }

    [Fact]
    public async Task InstructionsLoadAndCommitOnlyThroughExplicitBoundary()
    {
        var state = new QuickChatConversationState
        {
            AdditionalInstructions = "Existing instruction"
        };
        var session = new FakeSession(state);
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();

        Assert.Equal("Existing instruction", viewModel.AdditionalInstructions);
        viewModel.AdditionalInstructions = "Updated instruction";
        Assert.Equal(0, session.InstructionsUpdateCount);
        await viewModel.CommitAdditionalInstructionsAsync();

        Assert.Equal(1, session.InstructionsUpdateCount);
        Assert.Equal("Updated instruction", session.AdditionalInstructions);
        Assert.DoesNotContain(
            FloatingTools.App.Services.OpenAI
                .QuickChatOpenAiRequestBuilder.ProductInstruction,
            viewModel.AdditionalInstructions);
    }

    [Fact]
    public async Task CopyCopiesOnlyNonEmptyAssistantMessageText()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = CreateViewModel(clipboardService: clipboard);
        await viewModel.InitializeAsync();
        var text = new QuickChatMessageViewModel(
            Assistant("copy this", QuickChatMessageStatus.Completed));
        var user = new QuickChatMessageViewModel(User("do not copy"));
        var empty = new QuickChatMessageViewModel(
            Assistant("   ", QuickChatMessageStatus.Completed));

        viewModel.CopyMessage(text);
        viewModel.CopyMessage(user);
        viewModel.CopyMessage(empty);
        viewModel.CopyMessage(null);

        Assert.Equal(["copy this"], clipboard.Values);
        Assert.True(viewModel.CopyMessageCommand.CanExecute(text));
        Assert.False(viewModel.CopyMessageCommand.CanExecute(user));
        Assert.False(viewModel.CopyMessageCommand.CanExecute(empty));
    }

    [Fact]
    public async Task SessionNotificationsUseDispatcherBoundary()
    {
        var session = new FakeSession();
        var dispatcher = new RecordingDispatcher();
        var viewModel = CreateViewModel(session, dispatcher: dispatcher);
        await viewModel.InitializeAsync();
        var baseline = dispatcher.InvokeCount;
        var message = User("background");

        await Task.Run(() =>
        {
            session.AddMessage(message);
            session.RaiseMessageAdded(message);
        });

        Assert.True(dispatcher.InvokeCount > baseline);
        Assert.Equal("background", Assert.Single(viewModel.Messages).Text);
        Assert.Contains(dispatcher.InvokingThreadIds,
            threadId => threadId != Environment.CurrentManagedThreadId);
    }

    [Fact]
    public async Task DisposalUnsubscribesAndIgnoresLaterSessionChanges()
    {
        var session = new FakeSession();
        var viewModel = CreateViewModel(session);
        await viewModel.InitializeAsync();

        await viewModel.DisposeAsync();
        var late = User("late");
        session.AddMessage(late);
        session.RaiseMessageAdded(late);

        Assert.Equal(0, session.SubscriberCount);
        Assert.Empty(viewModel.Messages);
    }

    private static QuickChatViewModel CreateViewModel(
        FakeSession? session = null,
        IQuickChatImageStore? imageStore = null,
        IClipboardService? clipboardService = null,
        IClipboardImageProvider? clipboardImageProvider = null,
        IUiDispatcher? dispatcher = null) =>
        new(
            session ?? new FakeSession(),
            imageStore ?? new FakeImageStore(),
            clipboardService,
            clipboardImageProvider,
            dispatcher ?? new RecordingDispatcher());

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
            ErrorMessage = status == QuickChatMessageStatus.Error ? "safe error" : null,
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
            Width = 100,
            Height = 80
        };

    private static ManagedQuickChatImage Managed(
        string assetFileName,
        string mediaType,
        double width = 100,
        double height = 80) =>
        new(assetFileName, mediaType, width, height, $"C:\\managed\\{assetFileName}");

    private sealed class FakeSession : IActiveQuickChatConversation
    {
        private EventHandler<QuickChatConversationChangedEventArgs>? _changed;

        public FakeSession(QuickChatConversationState? state = null)
        {
            CurrentState = state ?? new QuickChatConversationState();
        }

        public event EventHandler<QuickChatConversationChangedEventArgs>? Changed
        {
            add
            {
                _changed += value;
                SubscriberCount++;
            }
            remove
            {
                _changed -= value;
                SubscriberCount--;
            }
        }

        public QuickChatConversationState CurrentState { get; private set; }

        public IReadOnlyList<QuickChatMessage> Messages => CurrentState.Messages;

        public string? AdditionalInstructions => CurrentState.AdditionalInstructions;

        public bool IsGenerating { get; private set; }

        public bool IsInitialized { get; private set; }

        public int SubscriberCount { get; private set; }

        public int InitializeCount { get; private set; }

        public int SendCount { get; private set; }

        public int StopCount { get; private set; }

        public int RetryCount { get; private set; }

        public int NewChatCount { get; private set; }

        public int InstructionsUpdateCount { get; private set; }

        public bool RejectSend { get; set; }

        public string? LastSendText { get; private set; }

        public IReadOnlyList<QuickChatAttachment> LastSendAttachments { get; private set; } = [];

        public List<(string? Text, IReadOnlyList<QuickChatAttachment> Attachments)>
            SendHistory { get; } = [];

        public Guid? LastRetryId { get; private set; }

        public List<string> DeletedUnsentAssets { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCount++;
            IsInitialized = true;
            _changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.Reset));
            return Task.CompletedTask;
        }

        public Task SendAsync(
            string? text,
            IReadOnlyList<QuickChatAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            if (RejectSend)
            {
                return Task.FromException(
                    new InvalidOperationException("send rejected"));
            }

            LastSendText = text;
            LastSendAttachments = attachments?.ToArray() ?? [];
            SendHistory.Add((text, LastSendAttachments));
            var user = User(text, [.. LastSendAttachments]);
            CurrentState.Messages.Add(user);
            RaiseMessageAdded(user);
            return Task.CompletedTask;
        }

        public Task RetryAsync(
            Guid assistantMessageId,
            CancellationToken cancellationToken = default)
        {
            RetryCount++;
            LastRetryId = assistantMessageId;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task SetAdditionalInstructionsAsync(
            string? additionalInstructions,
            CancellationToken cancellationToken = default)
        {
            InstructionsUpdateCount++;
            CurrentState.AdditionalInstructions = additionalInstructions;
            _changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.AdditionalInstructionsChanged));
            return Task.CompletedTask;
        }

        public Task DeleteUnsentAttachmentAsync(
            string assetFileName,
            CancellationToken cancellationToken = default)
        {
            DeletedUnsentAssets.Add(assetFileName);
            return Task.CompletedTask;
        }

        public Task NewChatAsync(CancellationToken cancellationToken = default)
        {
            NewChatCount++;
            CurrentState = new QuickChatConversationState
            {
                AdditionalInstructions = CurrentState.AdditionalInstructions
            };
            _changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.Reset));
            return Task.CompletedTask;
        }

        public Task PrepareForExitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void AddMessage(QuickChatMessage message) =>
            CurrentState.Messages.Add(message);

        public void RaiseMessageAdded(QuickChatMessage message) =>
            _changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.MessageAdded,
                    message));

        public void RaiseMessageUpdated(QuickChatMessage message) =>
            _changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.MessageUpdated,
                    message));

        public void SetGenerating(bool value)
        {
            IsGenerating = value;
            _changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.IsGeneratingChanged));
        }
    }

    private sealed class FakeImageStore(params ManagedQuickChatImage[] managed)
        : IQuickChatImageStore
    {
        private readonly Queue<ManagedQuickChatImage> _managed = new(managed);

        public string? ImportedFilePath { get; private set; }

        public int ImportFileCount { get; private set; }

        public byte[]? ImportedBytes { get; private set; }

        public Task<ManagedQuickChatImage> ImportFileAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            ImportedFilePath = sourcePath;
            ImportFileCount++;
            return Task.FromResult(_managed.Dequeue());
        }

        public Task<ManagedQuickChatImage> ImportBytesAsync(
            ReadOnlyMemory<byte> imageBytes,
            CancellationToken cancellationToken = default)
        {
            ImportedBytes = imageBytes.ToArray();
            return Task.FromResult(_managed.Dequeue());
        }

        public string GetAbsolutePath(string assetFileName) =>
            $"C:\\managed\\{assetFileName}";

        public IReadOnlyList<string> GetManagedAssetFileNames() => [];

        public Task DeleteAsync(
            string assetFileName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CapacityRaceImageStore(params ManagedQuickChatImage[] managed)
        : IQuickChatImageStore
    {
        private readonly ManagedQuickChatImage[] _managed = managed;
        private readonly TaskCompletionSource _releaseRace =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _nextIndex = -1;
        private int _racingImports;

        public async Task<ManagedQuickChatImage> ImportFileAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _nextIndex);
            if (index >= 2)
            {
                if (Interlocked.Increment(ref _racingImports) == 2)
                {
                    _releaseRace.TrySetResult();
                }

                await _releaseRace.Task.WaitAsync(cancellationToken);
            }

            return _managed[index];
        }

        public Task<ManagedQuickChatImage> ImportBytesAsync(
            ReadOnlyMemory<byte> imageBytes,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string GetAbsolutePath(string assetFileName) =>
            $"C:\\managed\\{assetFileName}";

        public IReadOnlyList<string> GetManagedAssetFileNames() => [];

        public Task DeleteAsync(
            string assetFileName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeClipboardImageProvider(byte[]? bytes)
        : IClipboardImageProvider
    {
        public int ReadCount { get; private set; }

        public byte[]? GetPngImage()
        {
            ReadCount++;
            return bytes;
        }
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public List<string> Values { get; } = [];

        public void SetText(string text) => Values.Add(text);
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public List<int> InvokingThreadIds { get; } = [];

        public bool CheckAccess() => true;

        public void Invoke(Action action)
        {
            InvokeCount++;
            InvokingThreadIds.Add(Environment.CurrentManagedThreadId);
            action();
        }
    }
}
