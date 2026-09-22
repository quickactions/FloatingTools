using System.Reflection;
using System.Runtime.CompilerServices;

namespace FloatingTools.Tests;

public sealed class AppStartupTests
{
    [Fact]
    public void OnStartup_IsAsyncSoSavedWordsLoadingDoesNotBlockTheUiDispatcher()
    {
        var method = typeof(FloatingTools.App.App).GetMethod(
            "OnStartup",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<AsyncStateMachineAttribute>());
    }

    [Fact]
    public void Startup_ComposesSharedAiInfrastructureForTranslation()
    {
        var source = File.ReadAllText(FindAppSourcePath());

        Assert.Contains("new WindowsDpapiAiCredentialStoreProvider", source);
        Assert.Contains("var aiConfigurationResolver = new AiConfigurationResolver", source);
        Assert.Contains("aiCredentialStores.GetStore(", source);
        Assert.Contains("AiCredentialScope.Application", source);
        Assert.Contains("new ApplicationOpenAiConfigurationProvider", source);
        Assert.Contains("new AiOpenAiConfigurationProvider", source);
        Assert.Contains("var applicationConnectionTester = new OpenAiConnectionTester", source);
        Assert.Contains("var translationConnectionTester = new OpenAiConnectionTester", source);
        Assert.Contains(
            "frequentWordsService,\n            openAiConfigurationProvider,\n            settings",
            source);
    }

    [Fact]
    public void Startup_ComposesRapidOcrFromBundledV5Models()
    {
        var source = File.ReadAllText(FindAppSourcePath());

        Assert.Contains("_localOcrService = new RapidOcrLocalOcrService(", source);
        Assert.Contains(
            "Path.Combine(AppContext.BaseDirectory, \"models\", \"v5\")",
            source);
        Assert.DoesNotContain("_localOcrService = new TesseractLocalOcrService(", source);
        Assert.Contains("new ScreenTextCaptureService(", source);
        Assert.Contains("            _localOcrService);", source);
    }

    [Fact]
    public void AppXaml_DeclaresAPermanentStaticColorsBaselineBeforeThemeServiceRuns()
    {
        // Regression guard for the C1 startup crash: WPF's compiled/optimized
        // template loader does not reliably resolve StaticResource references
        // against a color dictionary added to Application.Resources purely via
        // code — a real, statically-declared Colors.Dark.xaml entry must stay
        // in App.xaml so every StaticResource FloatingTools*/Brush* reference
        // in the app (converted to DynamicResource or not) can resolve, even
        // before ThemeService runs and even for views instantiated later.
        var appXaml = File.ReadAllText(FindAppXamlPath());
        var appCode = File.ReadAllText(FindAppSourcePath());

        Assert.Contains(
            "<ResourceDictionary Source=\"SharedUi/Tokens/Colors.Dark.xaml\" />",
            appXaml);
        Assert.DoesNotContain("Colors.xaml\"", appXaml);
        Assert.Contains("new ThemeService(", appCode);
    }

    private static string FindAppXamlPath() =>
        FindAppSourcePath().Replace(".xaml.cs", ".xaml", StringComparison.Ordinal);

    private static string FindAppSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "FloatingTools.App",
                "App.xaml.cs");
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate App.xaml.cs.");
    }
}
