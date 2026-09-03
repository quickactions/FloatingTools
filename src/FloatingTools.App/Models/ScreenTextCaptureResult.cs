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
    string? Text = null)
{
    public static ScreenTextCaptureResult Success(string text) =>
        new(ScreenTextCaptureStatus.Success, text);

    public static ScreenTextCaptureResult Cancelled() =>
        new(ScreenTextCaptureStatus.Cancelled);

    public static ScreenTextCaptureResult NoText() =>
        new(ScreenTextCaptureStatus.NoText);

    public static ScreenTextCaptureResult Failed() =>
        new(ScreenTextCaptureStatus.Failed);
}
