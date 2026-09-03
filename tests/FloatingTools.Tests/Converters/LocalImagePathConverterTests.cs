using System.Globalization;
using FloatingTools.App.Converters;

namespace FloatingTools.Tests.Converters;

public sealed class LocalImagePathConverterTests
{
    [Fact]
    public void OnLoadConversionReleasesSourceFileForPendingAttachmentDeletion()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"FloatingTools-ImageConverter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "pending.png");
        File.WriteAllBytes(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

        try
        {
            var converted = new LocalImagePathConverter().Convert(
                path,
                typeof(object),
                parameter: null,
                CultureInfo.InvariantCulture);

            Assert.NotNull(converted);
            File.Delete(path);
            Assert.False(File.Exists(path));
            GC.KeepAlive(converted);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            Directory.Delete(directory);
        }
    }
}
