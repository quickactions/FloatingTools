using FloatingTools.App.Models;

namespace FloatingTools.Tests.Models;

public sealed class AiSettingsTests
{
    [Fact]
    public void Defaults_RepresentIndependentCredentialAndModelInheritance()
    {
        var settings = new AiSettings();

        Assert.Equal("OpenAI", settings.DefaultProvider);
        Assert.Equal(OpenAiModelOptions.DefaultModel, settings.DefaultModel);
        Assert.NotSame(settings.Translation, settings.QuickChat);
        Assert.True(settings.Translation.UseAppCredentials);
        Assert.True(settings.QuickChat.UseAppCredentials);
        Assert.Null(settings.Translation.Provider);
        Assert.Null(settings.QuickChat.Provider);
        Assert.Null(settings.Translation.Model);
        Assert.Null(settings.QuickChat.Model);
    }

    [Fact]
    public void ToolModels_CanBePinnedIndependentlyWhileBothInheritCredentials()
    {
        var settings = new AiSettings();
        settings.Translation.Model = "translation-specific-model";

        Assert.Null(settings.QuickChat.Model);

        settings.QuickChat.Model = "quick-chat-specific-model";

        Assert.True(settings.Translation.UseAppCredentials);
        Assert.True(settings.QuickChat.UseAppCredentials);
        Assert.Equal("translation-specific-model", settings.Translation.Model);
        Assert.Equal("quick-chat-specific-model", settings.QuickChat.Model);
    }
}
