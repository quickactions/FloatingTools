using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IActiveQuickChatConversation
{
    event EventHandler<QuickChatConversationChangedEventArgs>? Changed;

    QuickChatConversationState CurrentState { get; }

    IReadOnlyList<QuickChatMessage> Messages { get; }

    string? AdditionalInstructions { get; }

    bool IsGenerating { get; }

    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SendAsync(
        string? text,
        IReadOnlyList<QuickChatAttachment>? attachments = null,
        CancellationToken cancellationToken = default);

    Task RetryAsync(
        Guid assistantMessageId,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SetAdditionalInstructionsAsync(
        string? additionalInstructions,
        CancellationToken cancellationToken = default);

    Task DeleteUnsentAttachmentAsync(
        string assetFileName,
        CancellationToken cancellationToken = default);

    Task NewChatAsync(CancellationToken cancellationToken = default);

    Task PrepareForExitAsync(CancellationToken cancellationToken = default);
}
