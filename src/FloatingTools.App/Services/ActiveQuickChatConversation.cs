using FloatingTools.App.Diagnostics;
using FloatingTools.App.Models;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.Services;

public sealed class ActiveQuickChatConversation : IActiveQuickChatConversation
{
    public static readonly TimeSpan DefaultPartialSaveInterval =
        TimeSpan.FromMilliseconds(500);

    private readonly IQuickChatStore _store;
    private readonly IQuickChatOpenAiService _openAiService;
    private readonly IQuickChatImageStore _imageStore;
    private readonly TimeSpan _partialSaveInterval;
    private readonly TimeProvider _timeProvider;
    private readonly QuickChatAttachmentContentCache? _attachmentContentCache;
    private readonly SemaphoreSlim _stateGate = new(1, 1);

    private CancellationTokenSource? _generationCancellation;
    private Task? _generationTask;
    private bool _initialized;
    private bool _exclusiveOperation;

    public ActiveQuickChatConversation(
        IQuickChatStore store,
        IQuickChatOpenAiService openAiService,
        IQuickChatImageStore imageStore,
        TimeSpan? partialSaveInterval = null,
        TimeProvider? timeProvider = null,
        QuickChatAttachmentContentCache? attachmentContentCache = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _openAiService = openAiService
            ?? throw new ArgumentNullException(nameof(openAiService));
        _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
        _partialSaveInterval = partialSaveInterval ?? DefaultPartialSaveInterval;
        if (_partialSaveInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(partialSaveInterval));
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _attachmentContentCache = attachmentContentCache;
    }

    public event EventHandler<QuickChatConversationChangedEventArgs>? Changed;

    public QuickChatConversationState CurrentState { get; private set; } = new();

    public IReadOnlyList<QuickChatMessage> Messages => CurrentState.Messages;

    public string? AdditionalInstructions => CurrentState.AdditionalInstructions;

    public bool IsGenerating { get; private set; }

    public bool IsInitialized => _initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            CurrentState = await _store.LoadAsync(cancellationToken);
            await DeleteUnreferencedAssetsAsync(CurrentState, cancellationToken);
            _initialized = true;
            RaiseChanged(QuickChatConversationChangeKind.Reset);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task SendAsync(
        string? text,
        IReadOnlyList<QuickChatAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        attachments ??= [];
        if (string.IsNullOrWhiteSpace(text) && attachments.Count == 0)
        {
            throw new ArgumentException(
                "A Quick Chat message requires text or an image attachment.",
                nameof(text));
        }

        using var timing = DebugAiRequestTiming.Start("quick_chat", "session_pipeline");
        timing.SetHasImages(attachments.Count > 0);
        Task generation;
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureCanStartGeneration();
            var userMessage = new QuickChatMessage
            {
                Id = Guid.NewGuid(),
                Role = QuickChatMessageRole.User,
                Text = text,
                Attachments = attachments.Select(CloneAttachment).ToList(),
                CreatedAt = _timeProvider.GetUtcNow()
            };
            CurrentState.Messages.Add(userMessage);
            RaiseChanged(QuickChatConversationChangeKind.MessageAdded, userMessage);

            var assistantMessage = new QuickChatMessage
            {
                Id = Guid.NewGuid(),
                Role = QuickChatMessageRole.Assistant,
                Text = string.Empty,
                CreatedAt = _timeProvider.GetUtcNow(),
                Status = QuickChatMessageStatus.InProgress
            };
            CurrentState.Messages.Add(assistantMessage);
            RaiseChanged(QuickChatConversationChangeKind.MessageAdded, assistantMessage);

            // Both messages are durably saved together, before HTTP starts,
            // in a single write — same crash-recovery/session-consistency
            // guarantee as saving them one at a time, half the disk I/O.
            await _store.SaveAsync(CurrentState, cancellationToken);
            timing.Mark("pending_exchange_persisted");
            generation = StartGenerationLocked(
                assistantMessage,
                cancellationToken,
                timing);
        }
        finally
        {
            _stateGate.Release();
        }

        await generation;
    }

    public async Task RetryAsync(
        Guid assistantMessageId,
        CancellationToken cancellationToken = default)
    {
        using var timing = DebugAiRequestTiming.Start("quick_chat", "retry_pipeline");
        Task generation;
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureCanStartGeneration();
            var index = CurrentState.Messages.FindIndex(
                message => message.Id == assistantMessageId);
            if (index <= 0
                || CurrentState.Messages[index].Role != QuickChatMessageRole.Assistant
                || CurrentState.Messages[index].Status != QuickChatMessageStatus.Error
                || CurrentState.Messages[index - 1].Role != QuickChatMessageRole.User)
            {
                throw new InvalidOperationException(
                    "Only a failed assistant response can be retried.");
            }

            var assistantMessage = CurrentState.Messages[index];
            timing.SetHasImages(CurrentState.Messages[index - 1].Attachments.Count > 0);
            assistantMessage.Text = string.Empty;
            assistantMessage.ErrorMessage = null;
            assistantMessage.Status = QuickChatMessageStatus.InProgress;
            RaiseChanged(QuickChatConversationChangeKind.MessageUpdated, assistantMessage);
            await _store.SaveAsync(CurrentState, cancellationToken);
            timing.Mark("retry_state_persisted");
            generation = StartGenerationLocked(
                assistantMessage,
                cancellationToken,
                timing);
        }
        finally
        {
            _stateGate.Release();
        }

        await generation;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? generation;
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            _generationCancellation?.Cancel();
            generation = _generationTask;
        }
        finally
        {
            _stateGate.Release();
        }

        if (generation is not null)
        {
            await generation.WaitAsync(cancellationToken);
        }
    }

    public async Task SetAdditionalInstructionsAsync(
        string? additionalInstructions,
        CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            if (_exclusiveOperation)
            {
                throw new InvalidOperationException(
                    "Quick Chat is currently resetting or preparing to exit.");
            }

            CurrentState.AdditionalInstructions = additionalInstructions;
            await _store.SaveAsync(CurrentState, cancellationToken);
            RaiseChanged(QuickChatConversationChangeKind.AdditionalInstructionsChanged);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task DeleteUnsentAttachmentAsync(
        string assetFileName,
        CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            if (GetReferencedAssetFileNames(CurrentState).Contains(assetFileName))
            {
                throw new InvalidOperationException(
                    "A persisted conversation attachment cannot be deleted as unsent.");
            }

            await _imageStore.DeleteAsync(assetFileName, cancellationToken);
            _attachmentContentCache?.Remove(assetFileName);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async Task NewChatAsync(CancellationToken cancellationToken = default)
    {
        var generation = await BeginExclusiveOperationAsync(cancellationToken);
        try
        {
            await AwaitExpectedGenerationEndAsync(generation, cancellationToken);
            await _stateGate.WaitAsync(cancellationToken);
            try
            {
                var previousAssets = GetReferencedAssetFileNames(CurrentState);
                var additionalInstructions = CurrentState.AdditionalInstructions;
                var freshState = new QuickChatConversationState
                {
                    AdditionalInstructions = additionalInstructions
                };
                await _store.SaveAsync(freshState, cancellationToken);
                CurrentState = freshState;
                RaiseChanged(QuickChatConversationChangeKind.Reset);
                foreach (var assetFileName in previousAssets)
                {
                    await _imageStore.DeleteAsync(assetFileName, cancellationToken);
                    _attachmentContentCache?.Remove(assetFileName);
                }
            }
            finally
            {
                _stateGate.Release();
            }
        }
        finally
        {
            await EndExclusiveOperationAsync();
        }
    }

    public async Task PrepareForExitAsync(CancellationToken cancellationToken = default)
    {
        var generation = await BeginExclusiveOperationAsync(cancellationToken);
        try
        {
            await AwaitExpectedGenerationEndAsync(generation, cancellationToken);
            await _stateGate.WaitAsync(cancellationToken);
            try
            {
                await _store.SaveAsync(CurrentState, CancellationToken.None);
            }
            finally
            {
                _stateGate.Release();
            }
        }
        finally
        {
            await EndExclusiveOperationAsync();
        }
    }

    private Task StartGenerationLocked(
        QuickChatMessage assistantMessage,
        CancellationToken callerCancellationToken,
        DebugAiRequestTiming timing)
    {
        _generationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callerCancellationToken);
        IsGenerating = true;
        RaiseChanged(QuickChatConversationChangeKind.IsGeneratingChanged);
        _generationTask = RunGenerationAsync(
            assistantMessage,
            _generationCancellation,
            timing);
        return _generationTask;
    }

    private async Task RunGenerationAsync(
        QuickChatMessage assistantMessage,
        CancellationTokenSource generationCancellation,
        DebugAiRequestTiming timing)
    {
        await Task.Yield();
        var completed = false;
        var firstDeltaPublished = false;
        var firstPartialStatePersisted = false;
        var outcome = "failure";
        DateTimeOffset? lastPartialSave = null;
        try
        {
            timing.Mark("stream_iteration_started");
            await foreach (var streamEvent in _openAiService.StreamResponseAsync(
                CurrentState,
                generationCancellation.Token))
            {
                if (streamEvent.Kind == QuickChatStreamEventKind.TextDelta)
                {
                    await _stateGate.WaitAsync(generationCancellation.Token);
                    try
                    {
                        assistantMessage.Text += streamEvent.Text;
                        RaiseChanged(
                            QuickChatConversationChangeKind.MessageUpdated,
                            assistantMessage);
                        if (!firstDeltaPublished)
                        {
                            firstDeltaPublished = true;
                            timing.Mark("first_delta_published_to_ui");
                        }

                        var now = _timeProvider.GetUtcNow();
                        if (lastPartialSave is null
                            || now - lastPartialSave >= _partialSaveInterval)
                        {
                            await _store.SaveAsync(
                                CurrentState,
                                generationCancellation.Token);
                            lastPartialSave = now;
                            if (!firstPartialStatePersisted)
                            {
                                firstPartialStatePersisted = true;
                                timing.Mark("first_partial_state_persisted");
                            }
                        }
                    }
                    finally
                    {
                        _stateGate.Release();
                    }
                }
                else if (streamEvent.Kind == QuickChatStreamEventKind.Completed)
                {
                    completed = true;
                    timing.Mark("stream_completion_received");
                }
            }

            if (!completed)
            {
                throw new QuickChatServiceException(
                    QuickChatFailureKind.InvalidResponse,
                    "The Quick Chat response ended before completion.");
            }

            await SetTerminalStateAsync(
                assistantMessage,
                QuickChatMessageStatus.Completed,
                errorMessage: null);
            timing.Mark("terminal_state_persisted");
            outcome = "success";
        }
        catch (OperationCanceledException) when (
            generationCancellation.IsCancellationRequested)
        {
            await SetTerminalStateAsync(
                assistantMessage,
                QuickChatMessageStatus.Interrupted,
                errorMessage: null);
            timing.Mark("terminal_state_persisted");
            outcome = "cancelled";
        }
        catch (Exception exception)
        {
            await SetTerminalStateAsync(
                assistantMessage,
                QuickChatMessageStatus.Error,
                CreateSafeErrorMessage(exception));
            timing.Mark("terminal_state_persisted");
            outcome = "failure";
            throw;
        }
        finally
        {
            await _stateGate.WaitAsync();
            try
            {
                if (ReferenceEquals(_generationCancellation, generationCancellation))
                {
                    _generationCancellation = null;
                    _generationTask = null;
                    IsGenerating = false;
                    RaiseChanged(QuickChatConversationChangeKind.IsGeneratingChanged);
                    timing.Mark("generation_state_cleared");
                }
            }
            finally
            {
                _stateGate.Release();
                generationCancellation.Dispose();
                timing.Complete(outcome);
            }
        }
    }

    private async Task SetTerminalStateAsync(
        QuickChatMessage assistantMessage,
        QuickChatMessageStatus status,
        string? errorMessage)
    {
        await _stateGate.WaitAsync();
        try
        {
            assistantMessage.Status = status;
            assistantMessage.ErrorMessage = errorMessage;
            RaiseChanged(QuickChatConversationChangeKind.MessageUpdated, assistantMessage);
            await _store.SaveAsync(CurrentState, CancellationToken.None);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async Task<Task?> BeginExclusiveOperationAsync(
        CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken);
        try
        {
            EnsureInitialized();
            if (_exclusiveOperation)
            {
                throw new InvalidOperationException(
                    "A Quick Chat reset or exit operation is already running.");
            }

            _exclusiveOperation = true;
            _generationCancellation?.Cancel();
            return _generationTask;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async Task EndExclusiveOperationAsync()
    {
        await _stateGate.WaitAsync();
        try
        {
            _exclusiveOperation = false;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private static async Task AwaitExpectedGenerationEndAsync(
        Task? generation,
        CancellationToken cancellationToken)
    {
        if (generation is null)
        {
            return;
        }

        try
        {
            await generation.WaitAsync(cancellationToken);
        }
        catch (QuickChatServiceException)
        {
            // The generation already persisted a safe Error state. Reset/exit
            // remains allowed to finish after an expected provider failure.
        }
    }

    private async Task DeleteUnreferencedAssetsAsync(
        QuickChatConversationState state,
        CancellationToken cancellationToken)
    {
        var referenced = GetReferencedAssetFileNames(state);
        foreach (var assetFileName in _imageStore.GetManagedAssetFileNames())
        {
            if (!referenced.Contains(assetFileName))
            {
                await _imageStore.DeleteAsync(assetFileName, cancellationToken);
            }
        }
    }

    private static HashSet<string> GetReferencedAssetFileNames(
        QuickChatConversationState state) =>
        state.Messages
            .SelectMany(message => message.Attachments)
            .Select(attachment => attachment.AssetFileName)
            .Where(assetFileName => !string.IsNullOrWhiteSpace(assetFileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void EnsureCanStartGeneration()
    {
        EnsureInitialized();
        if (_exclusiveOperation || IsGenerating)
        {
            throw new InvalidOperationException(
                "Quick Chat already has an active operation.");
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "Quick Chat must be initialized before use.");
        }
    }

    private static QuickChatAttachment CloneAttachment(QuickChatAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return new QuickChatAttachment
        {
            Id = attachment.Id,
            Type = attachment.Type,
            AssetFileName = attachment.AssetFileName,
            MediaType = attachment.MediaType,
            Width = attachment.Width,
            Height = attachment.Height
        };
    }

    private static string CreateSafeErrorMessage(Exception exception) =>
        exception is QuickChatServiceException serviceException
            ? serviceException.FailureKind switch
            {
                QuickChatFailureKind.NotConfigured =>
                    "OpenAI is not configured for Quick Chat.",
                QuickChatFailureKind.MissingManagedImage =>
                    "An attached image is missing or unavailable.",
                QuickChatFailureKind.InvalidResponse =>
                    "Quick Chat received an invalid response.",
                _ => "Quick Chat could not reach the AI service."
            }
            : "Quick Chat could not complete the request.";

    private void RaiseChanged(
        QuickChatConversationChangeKind kind,
        QuickChatMessage? message = null) =>
        Changed?.Invoke(this, new QuickChatConversationChangedEventArgs(kind, message));
}

public enum QuickChatConversationChangeKind
{
    Reset,
    MessageAdded,
    MessageUpdated,
    IsGeneratingChanged,
    AdditionalInstructionsChanged
}

public sealed class QuickChatConversationChangedEventArgs(
    QuickChatConversationChangeKind kind,
    QuickChatMessage? message = null) : EventArgs
{
    public QuickChatConversationChangeKind Kind { get; } = kind;

    public QuickChatMessage? Message { get; } = message;
}
