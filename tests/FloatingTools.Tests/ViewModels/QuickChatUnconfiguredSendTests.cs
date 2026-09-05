using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.Services.OpenAI;
using FloatingTools.App.ViewModels;

namespace FloatingTools.Tests.ViewModels;

/// <summary>
/// Regression coverage for the crash reported when Quick Chat is used without
/// an OpenAI key. The conversation classifies the failure and writes a safe
/// inline message onto the assistant turn, but it also rethrows; nothing
/// between there and AsyncRelayCommand used to catch it, so the send escaped as
/// an unhandled exception and terminated the application. These tests run the
/// real ActiveQuickChatConversation against a service that fails exactly the
/// way OpenAiQuickChatService does when no key is configured.
/// </summary>
public sealed class QuickChatUnconfiguredSendTests
{
    [Fact]
    public async Task Send_WithoutOpenAiConfiguration_DoesNotThrow()
    {
        var context = CreateContext();
        await context.ViewModel.InitializeAsync();
        context.ViewModel.DraftText = "hello";

        // The send must complete as a normal, handled outcome.
        await context.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, context.Service.CallCount);
        Assert.Null(context.ViewModel.SendCommand.ExecutionTask?.Exception);
    }

    [Fact]
    public async Task Send_WithoutOpenAiConfiguration_LeavesTheBusyStateCleared()
    {
        var context = CreateContext();
        await context.ViewModel.InitializeAsync();
        context.ViewModel.DraftText = "hello";

        await context.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.False(context.ViewModel.IsGenerating);
        Assert.False(context.Session.IsGenerating);
        Assert.False(context.ViewModel.SendCommand.IsRunning);
    }

    [Fact]
    public async Task Send_WithoutOpenAiConfiguration_ShowsTheFailureInlineOnTheAssistantTurn()
    {
        var context = CreateContext();
        await context.ViewModel.InitializeAsync();
        context.ViewModel.DraftText = "hello";

        await context.ViewModel.SendCommand.ExecuteAsync(null);

        var assistant = Assert.Single(
            context.ViewModel.Messages.Where(message => message.IsAssistant));
        Assert.Equal(QuickChatMessageStatus.Error, assistant.Status);
        Assert.Equal("OpenAI is not configured for Quick Chat.", assistant.ErrorMessage);
        Assert.True(assistant.CanRetry);
    }

    [Fact]
    public async Task Send_WithoutOpenAiConfiguration_KeepsQuickChatUsableAfterwards()
    {
        var context = CreateContext();
        await context.ViewModel.InitializeAsync();
        context.ViewModel.DraftText = "first";

        await context.ViewModel.SendCommand.ExecuteAsync(null);

        // The composer must accept input again rather than being stuck busy.
        context.ViewModel.DraftText = "second";
        Assert.True(context.ViewModel.CanSend);

        await context.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, context.Service.CallCount);
        Assert.False(context.ViewModel.IsGenerating);
    }

    [Fact]
    public async Task Retry_WithoutOpenAiConfiguration_DoesNotThrow()
    {
        var context = CreateContext();
        await context.ViewModel.InitializeAsync();
        context.ViewModel.DraftText = "hello";
        await context.ViewModel.SendCommand.ExecuteAsync(null);
        var assistant = Assert.Single(
            context.ViewModel.Messages.Where(message => message.IsAssistant));

        // Retrying the failed turn is the obvious next click and must not crash.
        await context.ViewModel.RetryCommand.ExecuteAsync(assistant);

        Assert.Equal(2, context.Service.CallCount);
        Assert.False(context.ViewModel.IsGenerating);
        Assert.Equal(QuickChatMessageStatus.Error, assistant.Status);
    }

    [Fact]
    public async Task Send_WhenConfigured_StillCompletesNormally()
    {
        var context = CreateContext(new StreamingStubService());
        await context.ViewModel.InitializeAsync();
        context.ViewModel.DraftText = "hello";

        await context.ViewModel.SendCommand.ExecuteAsync(null);

        var assistant = Assert.Single(
            context.ViewModel.Messages.Where(message => message.IsAssistant));
        Assert.Equal(QuickChatMessageStatus.Completed, assistant.Status);
        Assert.Null(assistant.ErrorMessage);
        Assert.False(context.ViewModel.IsGenerating);
    }

    private static Context CreateContext(IQuickChatOpenAiService? service = null)
    {
        var resolved = service ?? new UnconfiguredStubService();
        var session = new ActiveQuickChatConversation(
            new InMemoryQuickChatStore(),
            resolved,
            new UnusedImageStore());
        var viewModel = new QuickChatViewModel(
            session,
            new UnusedImageStore(),
            dispatcher: new ImmediateUiDispatcher());
        return new Context(
            viewModel,
            session,
            resolved as ICallCounting ?? new NullCallCounter());
    }

    private sealed record Context(
        QuickChatViewModel ViewModel,
        ActiveQuickChatConversation Session,
        ICallCounting Service);

    private interface ICallCounting
    {
        int CallCount { get; }
    }

    private sealed class NullCallCounter : ICallCounting
    {
        public int CallCount => 0;
    }

    /// <summary>Fails the way OpenAiQuickChatService does with no API key.</summary>
    private sealed class UnconfiguredStubService : IQuickChatOpenAiService, ICallCounting
    {
        public int CallCount { get; private set; }

        public async IAsyncEnumerable<QuickChatStreamEvent> StreamResponseAsync(
            QuickChatConversationState conversation,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.Yield();
            throw new QuickChatServiceException(
                QuickChatFailureKind.NotConfigured,
                "OpenAI is not configured for Quick Chat.");
#pragma warning disable CS0162 // Unreachable: required to make this an iterator.
            yield break;
#pragma warning restore CS0162
        }

        public Task<string> GetResponseAsync(
            QuickChatConversationState conversation,
            CancellationToken cancellationToken = default) =>
            throw new QuickChatServiceException(
                QuickChatFailureKind.NotConfigured,
                "OpenAI is not configured for Quick Chat.");
    }

    private sealed class StreamingStubService : IQuickChatOpenAiService, ICallCounting
    {
        public int CallCount { get; private set; }

        public async IAsyncEnumerable<QuickChatStreamEvent> StreamResponseAsync(
            QuickChatConversationState conversation,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.Yield();
            yield return new QuickChatStreamEvent(
                QuickChatStreamEventKind.TextDelta, "ok");
            yield return new QuickChatStreamEvent(QuickChatStreamEventKind.Completed);
        }

        public Task<string> GetResponseAsync(
            QuickChatConversationState conversation,
            CancellationToken cancellationToken = default) => Task.FromResult("ok");
    }

    private sealed class InMemoryQuickChatStore : IQuickChatStore
    {
        private QuickChatConversationState _state = new();

        public Task<QuickChatConversationState> LoadAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(_state);

        public Task SaveAsync(
            QuickChatConversationState state,
            CancellationToken cancellationToken = default)
        {
            _state = state;
            return Task.CompletedTask;
        }
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
