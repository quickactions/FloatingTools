namespace FloatingTools.App.Models;

public enum ScreenTextCaptureStatus
{
    Success,
    Cancelled,
    NoText,
    Failed
}

public sealed record ScreenTextCaptureResult(
    ScreenTextCaptureStatus Status,
    string? Text = null,
    bool SelectionCompleted = false)
{
    public static ScreenTextCaptureResult Success(string text) =>
        new(ScreenTextCaptureStatus.Success, text, SelectionCompleted: true);

    public static ScreenTextCaptureResult Cancelled() =>
        new(ScreenTextCaptureStatus.Cancelled);

    public static ScreenTextCaptureResult NoText() =>
        new(ScreenTextCaptureStatus.NoText, SelectionCompleted: true);

    public static ScreenTextCaptureResult Failed() =>
        new(ScreenTextCaptureStatus.Failed);
}
