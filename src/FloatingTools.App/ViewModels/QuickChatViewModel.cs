using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;

namespace FloatingTools.App.ViewModels;

public partial class QuickChatViewModel : ObservableObject, IAsyncDisposable
{
    public const int MaximumPendingAttachmentCount = 3;

    private readonly IActiveQuickChatConversation _session;
    private readonly IQuickChatImageStore _imageStore;
    private readonly IClipboardService _clipboardService;
    private readonly IClipboardImageProvider _clipboardImageProvider;
    private readonly IUiDispatcher _dispatcher;
    private readonly IOpenAiConfigurationProvider? _configurationProvider;
    private PendingSendTransfer? _pendingSendTransfer;
    private bool _disposed;

    [ObservableProperty]
    private string _draftText = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string? _additionalInstructions;

    [ObservableProperty]
    private bool _isNewChatConfirmationOpen;

    [ObservableProperty]
    private bool _isHeaderExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChatPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    private QuickChatPage _currentPage;

    /// <summary>
    /// Compact composer-level error, matching how Translation reports the same
    /// class of problem. A missing API key lives here instead of becoming a
    /// persisted assistant turn in the conversation feed.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    public QuickChatViewModel(
        IActiveQuickChatConversation session,
        IQuickChatImageStore imageStore,
        IClipboardService? clipboardService = null,
        IClipboardImageProvider? clipboardImageProvider = null,
        IUiDispatcher? dispatcher = null,
        QuickChatSettingsViewModel? settings = null,
        IOpenAiConfigurationProvider? configurationProvider = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
        _clipboardService = clipboardService ?? new NullClipboardService();
        _clipboardImageProvider = clipboardImageProvider
            ?? new NullClipboardImageProvider();
        _dispatcher = dispatcher ?? new SynchronizationContextUiDispatcher();
        _configurationProvider = configurationProvider;

        SendCommand = new AsyncRelayCommand(SendAsync, () => CanSend);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsGenerating);
        RetryCommand = new AsyncRelayCommand<QuickChatMessageViewModel>(
            RetryAsync,
            message => message?.CanRetry == true && !IsGenerating);
        Settings = settings;
        NewChatCommand = new AsyncRelayCommand(RequestNewChatAsync);
        ConfirmNewChatCommand = new AsyncRelayCommand(ConfirmNewChatAsync);
        CancelNewChatCommand = new RelayCommand(() => IsNewChatConfirmationOpen = false);
        ToggleHeaderCommand = new RelayCommand(() => IsHeaderExpanded = !IsHeaderExpanded);
        AttachImageFileCommand = new AsyncRelayCommand<string>(
            AttachImageFileCommandAsync,
            path => !string.IsNullOrWhiteSpace(path) && CanAttachImage);
        AttachClipboardImageCommand = new AsyncRelayCommand(
            AttachClipboardImageCommandAsync,
            () => CanAttachImage);
        RemovePendingAttachmentCommand =
            new AsyncRelayCommand<QuickChatPendingAttachmentViewModel>(
                RemovePendingAttachmentCommandAsync);
        CopyMessageCommand = new RelayCommand<QuickChatMessageViewModel>(
            CopyMessage,
            message => message?.CanCopy == true);
        CommitAdditionalInstructionsCommand = new AsyncRelayCommand(
            CommitAdditionalInstructionsAsync);

        PendingAttachments.CollectionChanged += (_, _) => RefreshComposerState();
        _session.Changed += OnSessionChanged;
    }

    public ObservableCollection<QuickChatMessageViewModel> Messages { get; } = [];

    public ObservableCollection<QuickChatPendingAttachmentViewModel>
        PendingAttachments { get; } = [];

    public QuickChatSettingsViewModel? Settings { get; }

    public bool HasConversationContent =>
        Messages.Count > 0
        || !string.IsNullOrWhiteSpace(DraftText)
        || PendingAttachments.Count > 0;

    public bool HasPendingAttachments => PendingAttachments.Count > 0;

    public bool CanAttachImage =>
        !_disposed && PendingAttachments.Count < MaximumPendingAttachmentCount;

    public bool IsChatPage => CurrentPage == QuickChatPage.Chat;

    public bool IsSettingsPage => CurrentPage == QuickChatPage.Settings;

    public bool CanSend =>
        !_disposed
        && IsInitialized
        && !IsGenerating
        && (!string.IsNullOrWhiteSpace(DraftText)
            || PendingAttachments.Count > 0);

    public IAsyncRelayCommand SendCommand { get; }

    public IAsyncRelayCommand StopCommand { get; }

    public IAsyncRelayCommand<QuickChatMessageViewModel> RetryCommand { get; }

    public IAsyncRelayCommand NewChatCommand { get; }

    public IAsyncRelayCommand ConfirmNewChatCommand { get; }

    public IRelayCommand CancelNewChatCommand { get; }

    public IRelayCommand ToggleHeaderCommand { get; }

    public IAsyncRelayCommand<string> AttachImageFileCommand { get; }

    public IAsyncRelayCommand AttachClipboardImageCommand { get; }

    public IAsyncRelayCommand<QuickChatPendingAttachmentViewModel>
        RemovePendingAttachmentCommand { get; }

    public IRelayCommand<QuickChatMessageViewModel> CopyMessageCommand { get; }

    public IAsyncRelayCommand CommitAdditionalInstructionsCommand { get; }

    public event EventHandler? ScrollFollowRequested;

    [RelayCommand]
    private void OpenSettings()
    {
        IsHeaderExpanded = false;
        CurrentPage = QuickChatPage.Settings;
    }

    [RelayCommand]
    private void BackToChat()
    {
        Settings?.CancelApiKeyEditCommand.Execute(null);
        IsHeaderExpanded = false;
        CurrentPage = QuickChatPage.Chat;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _session.InitializeAsync(cancellationToken);
        _dispatcher.Invoke(() =>
        {
            SynchronizeMessages();
            IsGenerating = _session.IsGenerating;
            AdditionalInstructions = _session.AdditionalInstructions;
            IsInitialized = true;
        });
    }

    public async Task SendAsync()
    {
        if (!CanSend)
        {
            return;
        }

        ErrorMessage = null;

        // A missing key can never produce a response, so the exchange is not
        // started at all: nothing is added to the conversation or persisted,
        // and the problem is reported next to the composer the way Translation
        // reports it. Retry is deliberately not offered — there is nothing to
        // retry until a key exists.
        if (_configurationProvider is not null
            && _configurationProvider.GetConfiguration() is null)
        {
            ErrorMessage = AiConfigurationMessages.MissingApiKey;
            return;
        }

        var text = DraftText;
        var pending = PendingAttachments.ToArray();
        var attachments = pending.Select(item => item.Attachment).ToArray();
        var transfer = new PendingSendTransfer(
            text,
            pending.Select(item => item.Id).ToHashSet());
        _pendingSendTransfer = transfer;

        try
        {
            var sendTask = _session.SendAsync(text, attachments);
            var first = await Task.WhenAny(transfer.Accepted.Task, sendTask);
            if (first == transfer.Accepted.Task)
            {
                ApplyAcceptedTransfer(transfer);
            }

            await sendTask;
            if (!transfer.IsApplied)
            {
                ApplyAcceptedTransfer(transfer);
            }
        }
        catch (QuickChatServiceException exception)
        {
            // The conversation has already written a safe, user-facing message
            // onto the assistant turn and cleared its generating state, so the
            // failure is shown inline. Rethrowing here would surface inside
            // AsyncRelayCommand as an unhandled exception and terminate the
            // application (this is what a missing OpenAI key used to do).
            if (transfer.Accepted.Task.IsCompletedSuccessfully && !transfer.IsApplied)
            {
                ApplyAcceptedTransfer(transfer);
            }

            // Reached only if configuration disappeared between the pre-send
            // check and the request; keep the composer wording consistent.
            if (exception.FailureKind == QuickChatFailureKind.NotConfigured)
            {
                ErrorMessage = AiConfigurationMessages.MissingApiKey;
            }
        }
        finally
        {
            if (ReferenceEquals(_pendingSendTransfer, transfer))
            {
                _pendingSendTransfer = null;
            }

            RefreshComposerState();
        }
    }

    public Task StopAsync() => _session.StopAsync();

    public async Task RetryAsync(QuickChatMessageViewModel? message)
    {
        if (message?.CanRetry != true || IsGenerating)
        {
            return;
        }

        try
        {
            await _session.RetryAsync(message.Id);
        }
        catch (QuickChatServiceException)
        {
            // Same boundary as SendAsync: the retried assistant turn already
            // carries the inline error, so the failure must not escape into
            // AsyncRelayCommand and take the application down.
        }
    }

    public async Task NewChatAsync()
    {
        ThrowIfDisposed();
        await _session.NewChatAsync();
        var pending = PendingAttachments.ToArray();
        await DeletePendingAssetsAsync(pending);
        _dispatcher.Invoke(() =>
        {
            DraftText = string.Empty;
            PendingAttachments.Clear();
            SynchronizeMessages();
            AdditionalInstructions = _session.AdditionalInstructions;
        });
    }

    private async Task RequestNewChatAsync()
    {
        IsHeaderExpanded = false;
        if (HasConversationContent)
        {
            IsNewChatConfirmationOpen = true;
            return;
        }

        await NewChatAsync();
    }

    private async Task ConfirmNewChatAsync()
    {
        IsNewChatConfirmationOpen = false;
        await NewChatAsync();
    }

    public async Task CommitAdditionalInstructionsAsync()
    {
        ThrowIfDisposed();
        await _session.SetAdditionalInstructionsAsync(AdditionalInstructions);
    }

    public async Task<bool> AttachImageFileAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!CanAttachImage)
        {
            return false;
        }

        var managed = await _imageStore.ImportFileAsync(sourcePath, cancellationToken);
        return await AddPendingImportedImageAsync(managed, cancellationToken);
    }

    public async Task<bool> AttachClipboardImageAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!CanAttachImage)
        {
            return false;
        }

        var pngBytes = _clipboardImageProvider.GetPngImage();
        if (pngBytes is null || pngBytes.Length == 0)
        {
            return false;
        }

        var managed = await _imageStore.ImportBytesAsync(pngBytes, cancellationToken);
        return await AddPendingImportedImageAsync(managed, cancellationToken);
    }

    public async Task RemovePendingAttachmentAsync(
        QuickChatPendingAttachmentViewModel? pending,
        CancellationToken cancellationToken = default)
    {
        if (pending is null || !PendingAttachments.Contains(pending))
        {
            return;
        }

        await _session.DeleteUnsentAttachmentAsync(
            pending.AssetFileName,
            cancellationToken);
        _dispatcher.Invoke(() => PendingAttachments.Remove(pending));
    }

    public void CopyMessage(QuickChatMessageViewModel? message)
    {
        if (message?.CanCopy == true)
        {
            _clipboardService.SetText(message.Text!);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Changed -= OnSessionChanged;
        QuickChatPendingAttachmentViewModel[] pending = [];
        _dispatcher.Invoke(() => pending = PendingAttachments.ToArray());
        await DeletePendingAssetsAsync(pending);
        _dispatcher.Invoke(() =>
        {
            PendingAttachments.Clear();
            RefreshComposerState();
        });
    }

    partial void OnDraftTextChanged(string value)
    {
        RefreshComposerState();
        OnPropertyChanged(nameof(HasConversationContent));
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInitializedChanged(bool value) => RefreshComposerState();

    private async Task AttachImageFileCommandAsync(string? sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            await AttachImageFileAsync(sourcePath);
        }
    }

    private async Task AttachClipboardImageCommandAsync() =>
        await AttachClipboardImageAsync();

    private async Task RemovePendingAttachmentCommandAsync(
        QuickChatPendingAttachmentViewModel? pending) =>
        await RemovePendingAttachmentAsync(pending);

    private async Task<bool> AddPendingImportedImageAsync(
        ManagedQuickChatImage managed,
        CancellationToken cancellationToken)
    {
        var pending = new QuickChatPendingAttachmentViewModel(
            new QuickChatAttachment
            {
                Id = Guid.NewGuid(),
                Type = QuickChatAttachmentType.Image,
                AssetFileName = managed.AssetFileName,
                MediaType = managed.MediaType,
                Width = managed.Width,
                Height = managed.Height
            },
            managed.AbsolutePath);
        var added = false;
        _dispatcher.Invoke(() =>
        {
            if (!_disposed
                && PendingAttachments.Count < MaximumPendingAttachmentCount)
            {
                PendingAttachments.Add(pending);
                added = true;
            }
        });

        if (!added)
        {
            await _session.DeleteUnsentAttachmentAsync(
                managed.AssetFileName,
                cancellationToken);
        }

        return added;
    }

    private void OnSessionChanged(
        object? sender,
        QuickChatConversationChangedEventArgs args)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            switch (args.Kind)
            {
                case QuickChatConversationChangeKind.Reset:
                    SynchronizeMessages();
                    break;
                case QuickChatConversationChangeKind.MessageAdded:
                    if (args.Message is not null)
                    {
                        AddOrUpdateMessage(args.Message);
                        CompletePendingTransferIfAccepted(args.Message);
                        OnPropertyChanged(nameof(HasConversationContent));
                        ScrollFollowRequested?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                case QuickChatConversationChangeKind.MessageUpdated:
                    if (args.Message is not null)
                    {
                        AddOrUpdateMessage(args.Message);
                        ScrollFollowRequested?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                case QuickChatConversationChangeKind.IsGeneratingChanged:
                    IsGenerating = _session.IsGenerating;
                    break;
                case QuickChatConversationChangeKind.AdditionalInstructionsChanged:
                    AdditionalInstructions = _session.AdditionalInstructions;
                    break;
            }
        });
    }

    private void SynchronizeMessages()
    {
        for (var targetIndex = 0; targetIndex < _session.Messages.Count; targetIndex++)
        {
            var message = _session.Messages[targetIndex];
            var existingIndex = FindMessageIndex(message.Id);
            if (existingIndex < 0)
            {
                Messages.Insert(targetIndex, CreateMessageViewModel(message));
            }
            else
            {
                var presentation = Messages[existingIndex];
                presentation.UpdateFrom(message);
                if (existingIndex != targetIndex)
                {
                    Messages.Move(existingIndex, targetIndex);
                }
            }
        }

        while (Messages.Count > _session.Messages.Count)
        {
            Messages.RemoveAt(Messages.Count - 1);
        }

        CopyMessageCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(HasConversationContent));
    }

    private void AddOrUpdateMessage(QuickChatMessage message)
    {
        var index = FindMessageIndex(message.Id);
        if (index < 0)
        {
            Messages.Add(CreateMessageViewModel(message));
        }
        else
        {
            Messages[index].UpdateFrom(message);
        }

        CopyMessageCommand.NotifyCanExecuteChanged();
    }

    private QuickChatMessageViewModel CreateMessageViewModel(QuickChatMessage message) =>
        new(message, _imageStore.GetAbsolutePath);

    private int FindMessageIndex(Guid id)
    {
        for (var index = 0; index < Messages.Count; index++)
        {
            if (Messages[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private void CompletePendingTransferIfAccepted(QuickChatMessage message)
    {
        var transfer = _pendingSendTransfer;
        if (transfer is null
            || message.Role != QuickChatMessageRole.User
            || !string.Equals(message.Text, transfer.Text, StringComparison.Ordinal))
        {
            return;
        }

        var messageAttachmentIds = message.Attachments
            .Select(attachment => attachment.Id)
            .ToHashSet();
        if (messageAttachmentIds.SetEquals(transfer.AttachmentIds))
        {
            transfer.Accepted.TrySetResult();
        }
    }

    private void ApplyAcceptedTransfer(PendingSendTransfer transfer)
    {
        _dispatcher.Invoke(() =>
        {
            if (transfer.IsApplied)
            {
                return;
            }

            transfer.IsApplied = true;
            if (string.Equals(DraftText, transfer.Text, StringComparison.Ordinal))
            {
                DraftText = string.Empty;
            }

            foreach (var pending in PendingAttachments
                .Where(item => transfer.AttachmentIds.Contains(item.Id))
                .ToArray())
            {
                PendingAttachments.Remove(pending);
            }

            OnPropertyChanged(nameof(HasConversationContent));
        });
    }

    private async Task DeletePendingAssetsAsync(
        IReadOnlyList<QuickChatPendingAttachmentViewModel> pending)
    {
        List<Exception>? failures = null;
        foreach (var item in pending)
        {
            try
            {
                await _session.DeleteUnsentAttachmentAsync(item.AssetFileName);
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(
                "One or more pending Quick Chat images could not be deleted.",
                failures);
        }
    }

    private void RefreshComposerState()
    {
        OnPropertyChanged(nameof(CanSend));
        OnPropertyChanged(nameof(HasConversationContent));
        OnPropertyChanged(nameof(HasPendingAttachments));
        OnPropertyChanged(nameof(CanAttachImage));
        SendCommand.NotifyCanExecuteChanged();
        AttachImageFileCommand.NotifyCanExecuteChanged();
        AttachClipboardImageCommand.NotifyCanExecuteChanged();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class PendingSendTransfer(
        string text,
        HashSet<Guid> attachmentIds)
    {
        public string Text { get; } = text;

        public HashSet<Guid> AttachmentIds { get; } = attachmentIds;

        public TaskCompletionSource Accepted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsApplied { get; set; }
    }
}
