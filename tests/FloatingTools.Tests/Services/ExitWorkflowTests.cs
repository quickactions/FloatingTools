using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class ExitWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_AwaitsPreparationBeforeClosingAndShuttingDown()
    {
        var releasePreparation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stages = new List<string>();
        var workflow = new ExitWorkflow(
        [
            new("Notes", async () =>
            {
                stages.Add("prepare");
                await releasePreparation.Task;
            })
        ],
        () => stages.Add("close"),
        () => stages.Add("shutdown"),
        (_, _) => throw new InvalidOperationException("Unexpected failure."));

        var execution = workflow.ExecuteAsync();

        Assert.False(execution.IsCompleted);
        Assert.Equal(["prepare"], stages);

        releasePreparation.SetResult();
        await execution;

        Assert.Equal(["prepare", "close", "shutdown"], stages);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatedRequestsShareOneIdempotentExecution()
    {
        var prepareCount = 0;
        var closeCount = 0;
        var shutdownCount = 0;
        var workflow = new ExitWorkflow(
        [
            new("prepare", () =>
            {
                prepareCount++;
                return Task.CompletedTask;
            })
        ],
        () => closeCount++,
        () => shutdownCount++,
        (_, _) => { });

        var first = workflow.ExecuteAsync();
        var second = workflow.ExecuteAsync();
        await Task.WhenAll(first, second);

        Assert.Same(first, second);
        Assert.Equal(1, prepareCount);
        Assert.Equal(1, closeCount);
        Assert.Equal(1, shutdownCount);
    }

    [Fact]
    public async Task PreparationFailure_DoesNotSkipOtherParticipantsOrFinalShutdown()
    {
        var stages = new List<string>();
        var failures = new List<string>();
        var workflow = new ExitWorkflow(
        [
            new("Notes", () => throw new IOException("Notes failed.")),
            new("Quick Chat", () =>
            {
                stages.Add("quick-chat");
                return Task.CompletedTask;
            }),
            new("Calendar", () =>
            {
                stages.Add("calendar");
                return Task.CompletedTask;
            })
        ],
        () => stages.Add("close"),
        () => stages.Add("shutdown"),
        (stage, _) => failures.Add(stage));

        await workflow.ExecuteAsync();

        Assert.Equal(["Notes"], failures);
        Assert.Equal(["quick-chat", "calendar", "close", "shutdown"], stages);
    }

    [Fact]
    public async Task WindowCloseFailure_StillRunsExplicitShutdown()
    {
        var shutdownCount = 0;
        var failures = new List<string>();
        var workflow = new ExitWorkflow(
            [],
            () => throw new InvalidOperationException("Close failed."),
            () => shutdownCount++,
            (stage, _) => failures.Add(stage));

        await workflow.ExecuteAsync();

        Assert.Equal(1, shutdownCount);
        Assert.Equal(["window close"], failures);
    }
}
