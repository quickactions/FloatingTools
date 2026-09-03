using System.Net;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services.OpenAI;

public interface IQuickChatOpenAiService
{
    IAsyncEnumerable<QuickChatStreamEvent> StreamResponseAsync(
        QuickChatConversationState conversation,
        CancellationToken cancellationToken = default);

    Task<string> GetResponseAsync(
        QuickChatConversationState conversation,
        CancellationToken cancellationToken = default);
}

public enum QuickChatStreamEventKind
{
    TextDelta,
    Completed
}

public sealed record QuickChatStreamEvent(
    QuickChatStreamEventKind Kind,
    string? Text = null);

public enum QuickChatFailureKind
{
    NotConfigured,
    HttpFailure,
    InvalidResponse,
    MissingManagedImage
}

public sealed class QuickChatServiceException : Exception
{
    public QuickChatServiceException(
        QuickChatFailureKind failureKind,
        string message,
        Exception? innerException = null,
        HttpStatusCode? statusCode = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        StatusCode = statusCode;
    }

    public QuickChatFailureKind FailureKind { get; }

    public HttpStatusCode? StatusCode { get; }
}
