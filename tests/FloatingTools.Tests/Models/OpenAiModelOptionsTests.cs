using FloatingTools.App.Models;

namespace FloatingTools.Tests.Models;

public sealed class OpenAiModelOptionsTests
{
    [Fact]
    public void Supported_ContainsOnlySolTerraAndLunaInDisplayOrder()
    {
        Assert.Equal(
            [
                OpenAiModelOptions.SolModel,
                OpenAiModelOptions.TerraModel,
                OpenAiModelOptions.LunaModel
            ],
            OpenAiModelOptions.Supported);
        Assert.DoesNotContain(
            OpenAiModelOptions.LegacyNanoModel,
            OpenAiModelOptions.Supported);
    }

    [Fact]
    public void Normalize_PreservesPersistedNanoWithoutMakingItSupported()
    {
        Assert.Equal(
            OpenAiModelOptions.LegacyNanoModel,
            OpenAiModelOptions.Normalize(OpenAiModelOptions.LegacyNanoModel));
        Assert.DoesNotContain(
            OpenAiModelOptions.LegacyNanoModel,
            OpenAiModelOptions.Supported);
    }

    [Fact]
    public void DefaultModel_RemainsLuna()
    {
        Assert.Equal(OpenAiModelOptions.LunaModel, OpenAiModelOptions.DefaultModel);
    }
}
