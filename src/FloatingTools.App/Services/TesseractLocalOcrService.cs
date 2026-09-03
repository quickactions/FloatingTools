using System.IO;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractImage = TesseractOCR.Pix.Image;

namespace FloatingTools.App.Services;

public sealed class TesseractLocalOcrService : ILocalOcrService
{
    private readonly string _dataPath;
    private readonly object _sync = new();
    private Engine? _engine;
    private bool _disposed;

    public TesseractLocalOcrService(string dataPath)
    {
        _dataPath = dataPath
            ?? throw new ArgumentNullException(nameof(dataPath));
    }

    public Task<string> RecognizeEnglishAsync(
        CapturedScreenImage image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var bytes = image.Data.ToArray();
        return Task.Run(
            () =>
            {
                try
                {
                    lock (_sync)
                    {
                        ObjectDisposedException.ThrowIf(_disposed, this);
                        return Recognize(bytes, cancellationToken);
                    }
                }
                finally
                {
                    Array.Clear(bytes);
                }
            });
    }

    private string Recognize(byte[] encodedImage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDataFileExists();
        _engine ??= new Engine(_dataPath, Language.English, EngineMode.LstmOnly);

        using var image = TesseractImage.LoadFromMemory(encodedImage);
        using var page = _engine.Process(image, PageSegMode.SingleBlock);
        cancellationToken.ThrowIfCancellationRequested();
        return OcrTextNormalizer.Normalize(page.Text);
    }

    private void EnsureDataFileExists()
    {
        var englishData = Path.Combine(_dataPath, "eng.traineddata");
        if (!File.Exists(englishData))
        {
            throw new FileNotFoundException(
                "The local English OCR data is missing.",
                englishData);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            _disposed = true;
            _engine?.Dispose();
            _engine = null;
        }
    }
}
