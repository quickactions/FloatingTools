namespace FloatingTools.App.Services;

public enum TranslationFailureKind
{
    Unauthorized,
    Timeout,
    NetworkUnavailable,
    QuotaExceeded,
    ProviderUnavailable,
    IncompleteResponse,
    InvalidResponse
}

public sealed class TranslationServiceException : Exception
{
    public TranslationServiceException(
        TranslationFailureKind failureKind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    public TranslationFailureKind FailureKind { get; }
}
