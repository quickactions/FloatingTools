using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IQuickChatStore
{
    Task<QuickChatConversationState> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        QuickChatConversationState state,
        CancellationToken cancellationToken = default);
}
