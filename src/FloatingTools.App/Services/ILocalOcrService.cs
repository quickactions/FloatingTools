namespace FloatingTools.App.Services;

public interface ILocalOcrService : IDisposable
{
    Task<string> RecognizeEnglishAsync(
        CapturedScreenImage image,
        CancellationToken cancellationToken = default);
}

public sealed class CapturedScreenImage : IDisposable
{
    private byte[]? _encodedBytes;

    public CapturedScreenImage(byte[] encodedBytes)
    {
        ArgumentNullException.ThrowIfNull(encodedBytes);
        if (encodedBytes.Length == 0)
        {
            throw new ArgumentException("Captured image data cannot be empty.", nameof(encodedBytes));
        }

        _encodedBytes = encodedBytes;
    }

    public ReadOnlyMemory<byte> Data => _encodedBytes
        ?? throw new ObjectDisposedException(nameof(CapturedScreenImage));

    public void Dispose()
    {
        var bytes = Interlocked.Exchange(ref _encodedBytes, null);
        if (bytes is not null)
        {
            Array.Clear(bytes);
        }
    }
}
