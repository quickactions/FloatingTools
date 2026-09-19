using System.IO;
using RapidOcrNet;
using SkiaSharp;

namespace FloatingTools.App.Services;

public sealed class RapidOcrLocalOcrService : ILocalOcrService
{
    private const string DetectorModel = "ch_PP-OCRv5_mobile_det.onnx";
    private const string ClassifierModel = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
    private const string RecognizerModel = "latin_PP-OCRv5_rec_mobile_infer.onnx";
    private const string DictionaryFile = "ppocrv5_latin_dict.txt";

    private readonly string _modelsDirectory;
    private readonly object _sync = new();
    private RapidOcr? _ocr;
    private bool _disposed;

    public RapidOcrLocalOcrService(string modelsDirectory)
    {
        _modelsDirectory = modelsDirectory
            ?? throw new ArgumentNullException(nameof(modelsDirectory));
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
        EnsureInitialized();

        using var decoded = SKBitmap.Decode(encodedImage)
            ?? throw new InvalidDataException("The captured image could not be decoded as a bitmap.");
        using var recognitionBitmap = CreateOpaqueBitmapIfNeeded(decoded);
        var result = _ocr!.Detect(
            recognitionBitmap ?? decoded,
            RapidOcrOptions.Default,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return OcrTextNormalizer.Normalize(result.StrRes);
    }

    private void EnsureInitialized()
    {
        if (_ocr is not null)
        {
            return;
        }

        var detectorPath = RequireModel(DetectorModel);
        var classifierPath = RequireModel(ClassifierModel);
        var recognizerPath = RequireModel(RecognizerModel);
        var dictionaryPath = RequireModel(DictionaryFile);

        var ocr = new RapidOcr();
        try
        {
            ocr.InitModels(
                detectorPath,
                classifierPath,
                recognizerPath,
                dictionaryPath);
            _ocr = ocr;
        }
        catch
        {
            ocr.Dispose();
            throw;
        }
    }

    private string RequireModel(string fileName)
    {
        var path = Path.Combine(_modelsDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The RapidOCR model file is missing: {path}",
                path);
        }

        return path;
    }

    private static SKBitmap? CreateOpaqueBitmapIfNeeded(SKBitmap source)
    {
        if (source.AlphaType == SKAlphaType.Opaque)
        {
            return null;
        }

        // Screen-capture BMPs can carry an unused alpha channel. RapidOCR's
        // Skia pipeline expects visible pixels, so retain RGB and force alpha
        // opaque without otherwise preprocessing the captured image.
        var opaque = new SKBitmap(
            new SKImageInfo(
                source.Width,
                source.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Opaque));
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                opaque.SetPixel(x, y, new SKColor(color.Red, color.Green, color.Blue));
            }
        }

        return opaque;
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
            _ocr?.Dispose();
            _ocr = null;
        }
    }
}
