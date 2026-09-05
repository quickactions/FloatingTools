using System.Xml.Linq;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

/// <summary>
/// Translation and Quick Chat must report a missing OpenAI key the same way: a
/// compact inline status next to the composer, using identical wording, and
/// without turning the failure into persisted conversation/history content.
/// </summary>
public sealed class AiErrorPresentationConsistencyTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MissingKeyWording_IsSharedByBothTools()
    {
        Assert.Equal(
            "OpenAI API key is missing. Add it in Settings.",
            AiConfigurationMessages.MissingApiKey);
        Assert.Equal(
            AiConfigurationMessages.MissingApiKey,
            new TranslationProviderNotConfiguredException().Message);
    }

    [Fact]
    public async Task QuickChat_SendWithoutConfiguration_ReportsInlineAndPersistsNothing()
    {
        var session = new CountingSession();
        var viewModel = new QuickChatViewModel(
            session,
            new UnusedImageStore(),
            dispatcher: new ImmediateUiDispatcher(),
            configurationProvider: new TestOpenAiConfigurationProvider());
        await viewModel.InitializeAsync();
        viewModel.DraftText = "hello";

        await viewModel.SendCommand.ExecuteAsync(null);

        // Inline, next to the composer — same wording Translation shows.
        Assert.Equal(AiConfigurationMessages.MissingApiKey, viewModel.ErrorMessage);

        // Nothing was sent, nothing entered the feed, nothing was persisted.
        Assert.Equal(0, session.SendCount);
        Assert.Empty(viewModel.Messages);
        Assert.Empty(session.CurrentState.Messages);

        // The draft is kept so the user can send it once a key exists.
        Assert.Equal("hello", viewModel.DraftText);
        Assert.False(viewModel.IsGenerating);
    }

    [Fact]
    public async Task QuickChat_ConfiguredSend_IsUnaffectedAndClearsAnyPreviousError()
    {
        var session = new CountingSession();
        var provider = new SwitchableConfigurationProvider();
        var viewModel = new QuickChatViewModel(
            session,
            new UnusedImageStore(),
            dispatcher: new ImmediateUiDispatcher(),
            configurationProvider: provider);
        await viewModel.InitializeAsync();

        viewModel.DraftText = "first";
        await viewModel.SendCommand.ExecuteAsync(null);
        Assert.Equal(AiConfigurationMessages.MissingApiKey, viewModel.ErrorMessage);

        provider.Configuration = new OpenAiTranslationConfiguration("key", "model");
        viewModel.DraftText = "second";
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(1, session.SendCount);
        Assert.Equal("second", session.LastSendText);
    }

    [Fact]
    public void QuickChatView_RendersTheInlineErrorNextToTheComposerLikeTranslation()
    {
        var quickChat = XDocument.Load(FindViewPath("QuickChatToolView.xaml"));
        var errorText = quickChat.Descendants(Presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(X + "Name") == "ComposerErrorText");

        Assert.Equal("{Binding ErrorMessage}", (string?)errorText.Attribute("Text"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushStatusError}",
            (string?)errorText.Attribute("Foreground"));
        Assert.Equal("Wrap", (string?)errorText.Attribute("TextWrapping"));

        // It must sit with the composer, not inside the conversation feed.
        Assert.Contains(
            errorText.Ancestors(Presentation + "StackPanel"),
            panel => panel.Descendants(Presentation + "Border").Any(
                border => (string?)border.Attribute(X + "Name") == "ComposerContainer"));
        Assert.DoesNotContain(
            errorText.Ancestors(Presentation + "ScrollViewer"),
            viewer => (string?)viewer.Attribute(X + "Name") == "ConversationScrollViewer");
    }

    private static string FindViewPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(
                    directory.FullName, "src", "FloatingTools.App", "Views", fileName);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }

    private sealed class SwitchableConfigurationProvider : IOpenAiConfigurationProvider
    {
        public OpenAiTranslationConfiguration? Configuration { get; set; }

        public OpenAiTranslationConfiguration? GetConfiguration() => Configuration;
    }

    private sealed class CountingSession : IActiveQuickChatConversation
    {
        public event EventHandler<QuickChatConversationChangedEventArgs>? Changed;

        public QuickChatConversationState CurrentState { get; } = new();

        public IReadOnlyList<QuickChatMessage> Messages => CurrentState.Messages;

        public string? AdditionalInstructions => null;

        public bool IsGenerating => false;

        public bool IsInitialized { get; private set; }

        public int SendCount { get; private set; }

        public string? LastSendText { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            Changed?.Invoke(
                this,
                new QuickChatConversationChangedEventArgs(
                    QuickChatConversationChangeKind.Reset));
            return Task.CompletedTask;
        }

        public Task SendAsync(
            string? text,
            IReadOnlyList<QuickChatAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            LastSendText = text;
            return Task.CompletedTask;
        }

        public Task RetryAsync(
            Guid assistantMessageId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NewChatAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAdditionalInstructionsAsync(
            string? instructions,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteUnsentAttachmentAsync(
            string assetFileName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PrepareForExitAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class UnusedImageStore : IQuickChatImageStore
    {
        public Task<ManagedQuickChatImage> ImportFileAsync(
            string sourcePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ManagedQuickChatImage> ImportBytesAsync(
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string GetAbsolutePath(string assetFileName) => assetFileName;

        public IReadOnlyList<string> GetManagedAssetFileNames() => [];

        public Task DeleteAsync(
            string assetFileName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Invoke(Action action) => action();
    }
}
