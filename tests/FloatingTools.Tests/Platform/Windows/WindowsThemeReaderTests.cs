using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Platform.Windows;

public sealed class WindowsThemeReaderTests
{
    [Fact]
    public void ReadCurrentTheme_ValueOfOne_ReturnsLight()
    {
        var reader = new WindowsThemeReader(() => 1);

        Assert.Equal(AppTheme.Light, reader.ReadCurrentTheme());
    }

    [Fact]
    public void ReadCurrentTheme_ValueOfZero_ReturnsDark()
    {
        var reader = new WindowsThemeReader(() => 0);

        Assert.Equal(AppTheme.Dark, reader.ReadCurrentTheme());
    }

    [Fact]
    public void ReadCurrentTheme_MissingValue_FallsBackToDark()
    {
        var reader = new WindowsThemeReader(() => null);

        Assert.Equal(AppTheme.Dark, reader.ReadCurrentTheme());
    }

    [Fact]
    public void ReadCurrentTheme_UnexpectedValueType_FallsBackToDark()
    {
        var reader = new WindowsThemeReader(() => "not-an-int");

        Assert.Equal(AppTheme.Dark, reader.ReadCurrentTheme());
    }

    [Fact]
    public void ReadCurrentTheme_ReadThrows_FallsBackToDarkWithoutThrowing()
    {
        var reader = new WindowsThemeReader(() =>
            throw new InvalidOperationException("registry unavailable"));

        var theme = reader.ReadCurrentTheme();

        Assert.Equal(AppTheme.Dark, theme);
    }
}
