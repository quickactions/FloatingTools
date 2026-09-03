namespace FloatingTools.App.Services;

public interface IClipboardService
{
    void SetText(string text);
}

public sealed class NullClipboardService : IClipboardService
{
    public void SetText(string text)
    {
    }
}
