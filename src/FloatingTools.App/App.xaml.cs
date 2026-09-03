using System.IO;
using System.Net.Http;
using System.Windows;
#if DEBUG
using FloatingTools.App.Debugging;
#endif
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Models;
using FloatingTools.App.Services.Ai;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;

namespace FloatingTools.App;

public partial class App : Application
{
    private HttpClient? _translationHttpClient;
    private HttpClient? _quickChatHttpClient;
    private ILocalOcrService? _localOcrService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _translationHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _quickChatHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            "settings.json");
        var settingsService = new SettingsService(settingsPath);
        var settings = settingsService.Load();

        var appAppearanceResolver = new AppAppearanceResolver(
            () => new WindowsThemeReader().ReadCurrentTheme());
        // ThemeService appends its own managed Colors.Dark/Light.xaml
        // dictionary after App.xaml's permanent Colors.Dark.xaml baseline
        // (see App.xaml) — being last in merge order, it's what
        // DynamicResource-bound content actually resolves against.
        var themeService = new ThemeService(
            settings.Appearance,
            appAppearanceResolver,
            new WindowsThemeWatcher(),
            Resources);
        var credentialDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools");
        IAiCredentialStoreProvider aiCredentialStores = new CachingAiCredentialStoreProvider(
            new WindowsDpapiAiCredentialStoreProvider(credentialDirectory));
        var aiConfigurationResolver = new AiConfigurationResolver(
            settings,
            aiCredentialStores);
        var secureApiKeyStore = aiCredentialStores.GetStore(
            AiCredentialScope.Application);
        var translationCredentialStore = aiCredentialStores.GetStore(
            AiCredentialScope.Translation);
        var quickChatCredentialStore = aiCredentialStores.GetStore(
            AiCredentialScope.QuickChat);
        var applicationOpenAiConfigurationProvider =
            new ApplicationOpenAiConfigurationProvider(
                settings,
                secureApiKeyStore);
        var openAiConfigurationProvider = new AiOpenAiConfigurationProvider(
            aiConfigurationResolver);
        var quickChatConfigurationProvider = new AiOpenAiConfigurationProvider(
            aiConfigurationResolver,
            AiToolId.QuickChat);
        ITranslationService translationService = new OpenAiTranslationService(
            _translationHttpClient,
            openAiConfigurationProvider);
        var applicationConnectionTester = new OpenAiConnectionTester(
            _translationHttpClient,
            applicationOpenAiConfigurationProvider);
        var translationConnectionTester = new OpenAiConnectionTester(
            _translationHttpClient,
            openAiConfigurationProvider);
        var quickChatConnectionTester = new OpenAiConnectionTester(
            _quickChatHttpClient,
            quickChatConfigurationProvider);
        var placementService = new WindowPlacementService();
        _localOcrService = new TesseractLocalOcrService(
            Path.Combine(AppContext.BaseDirectory, "tessdata"));
        var screenTextCaptureService = new ScreenTextCaptureService(
            placementService,
            new WindowsScreenRegionCaptureService(),
            _localOcrService);
        var translationHistoryStore = new InMemoryTranslationHistoryStore();
        var savedWordsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            "saved-words.json");
        var savedWordsService = new SavedWordsService(
            new JsonSavedWordsStore(savedWordsPath));
        await savedWordsService.InitializeAsync();
        var frequentWordsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            "frequent-words.json");
        var frequentWordsService = new FrequentWordsService(
            new JsonFrequentWordsStore(frequentWordsPath));
        await frequentWordsService.InitializeAsync();
        var notesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            "notes.json");
        var notesAssetsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloatingTools",
            "notes-assets");
        var notesToolViewModel = new NotesToolViewModel(
            new JsonNotesStore(notesPath),
            imageStore: new LocalNotesImageStore(notesAssetsPath),
            clipboardService: new WindowsClipboardService(),
            linkLauncher: new WindowsNoteLinkLauncher(),
            exportService: new WindowsNoteExportService());
        await notesToolViewModel.InitializeAsync();
        var quickChatImageStore = new LocalQuickChatImageStore();
        var quickChatAttachmentContentCache = new QuickChatAttachmentContentCache();
        var activeQuickChatConversation = new ActiveQuickChatConversation(
            new JsonQuickChatStore(),
            new OpenAiQuickChatService(
                _quickChatHttpClient,
                quickChatConfigurationProvider,
                new QuickChatOpenAiRequestBuilder(
                    quickChatImageStore,
                    quickChatAttachmentContentCache)),
            quickChatImageStore,
            attachmentContentCache: quickChatAttachmentContentCache);
        var quickChatSettingsViewModel = new QuickChatSettingsViewModel(
            settings,
            settingsService,
            quickChatCredentialStore,
            quickChatConfigurationProvider,
            quickChatConnectionTester);
        var quickChatViewModel = new QuickChatViewModel(
            activeQuickChatConversation,
            quickChatImageStore,
            new WindowsClipboardService(),
            new WindowsClipboardImageProvider(),
            new SynchronizationContextUiDispatcher(),
            quickChatSettingsViewModel);
        var translationToolViewModel = new TranslationToolViewModel(
            translationService,
            translationHistoryStore,
            new WindowsClipboardService(),
            savedWordsService,
            new WindowsSavedWordsExportService(),
            frequentWordsService,
            openAiConfigurationProvider,
            settings,
            settingsService,
            secureApiKeyStore,
            translationConnectionTester,
            screenTextCaptureService,
            translationCredentialStore,
            applicationOpenAiConfigurationProvider,
            applicationConnectionTester);
#if DEBUG
        DebugTranslationFeedSeeder.Seed(translationToolViewModel);
#endif
        var viewModel = new FloatingToolbarViewModel(
            settings.LastUsedTool,
            settings.ActiveToolPanelSize);
        var calendarToolViewModel = new CalendarToolViewModel(
            settings,
            settingsService,
            new CalendarLanguageResolver(settings),
            new HebrewCalendarHolidayProvider(),
            new JsonCalendarStore(),
            new WindowsClipboardService());
        translationToolViewModel.Settings.ApplicationLanguageChanged +=
            _ => calendarToolViewModel.RefreshApplicationLanguage();
        translationToolViewModel.Settings.AppearanceChanged +=
            themeService.ApplyAppearance;
        await calendarToolViewModel.InitializeAsync();
        var toolbarWindow = new ToolbarWindow(
            placementService,
            settingsService,
            settings);
        var panelWindow = new PanelWindow(
            viewModel,
            translationToolViewModel,
            notesToolViewModel,
            quickChatViewModel,
            calendarToolViewModel,
            translationToolViewModel.Settings);
        var coordinator = new WindowCoordinator(
            toolbarWindow,
            panelWindow,
            viewModel,
            placementService,
            settingsService,
            settings,
            notesToolViewModel,
            activeQuickChatConversation,
            quickChatViewModel,
            calendarToolViewModel,
            translationToolViewModel,
            new GlobalHotkeyService(),
            themeService);

        MainWindow = toolbarWindow;
        coordinator.ShowToolbar();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _localOcrService?.Dispose();
        _translationHttpClient?.Dispose();
        _quickChatHttpClient?.Dispose();
        base.OnExit(e);
    }
}
