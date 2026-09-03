using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class AppAppearanceResolverTests
{
    [Theory]
    [InlineData(AppAppearanceMode.Dark, AppTheme.Light, AppTheme.Dark)]
    [InlineData(AppAppearanceMode.Light, AppTheme.Dark, AppTheme.Light)]
    [InlineData(AppAppearanceMode.System, AppTheme.Dark, AppTheme.Dark)]
    [InlineData(AppAppearanceMode.System, AppTheme.Light, AppTheme.Light)]
    [InlineData((AppAppearanceMode)999, AppTheme.Light, AppTheme.Light)]
    public void Resolve_UsesExplicitPreferenceOrCurrentSystemTheme(
        AppAppearanceMode mode,
        AppTheme currentSystemTheme,
        AppTheme expected)
    {
        var resolver = new AppAppearanceResolver(() => currentSystemTheme);

        Assert.Equal(expected, resolver.Resolve(mode));
    }

    [Fact]
    public void Resolve_ExplicitDarkOrLight_NeverInvokesTheSystemThemeDelegate()
    {
        var invoked = false;
        var resolver = new AppAppearanceResolver(() =>
        {
            invoked = true;
            return AppTheme.Light;
        });

        resolver.Resolve(AppAppearanceMode.Dark);
        resolver.Resolve(AppAppearanceMode.Light);

        Assert.False(invoked);
    }

    [Fact]
    public void Resolve_System_AlwaysReadsTheCurrentValueRatherThanCaching()
    {
        var current = AppTheme.Dark;
        var resolver = new AppAppearanceResolver(() => current);

        var first = resolver.Resolve(AppAppearanceMode.System);
        current = AppTheme.Light;
        var second = resolver.Resolve(AppAppearanceMode.System);

        Assert.Equal(AppTheme.Dark, first);
        Assert.Equal(AppTheme.Light, second);
    }
}
