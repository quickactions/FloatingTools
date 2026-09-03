namespace FloatingTools.App.Services.OpenAI;

public sealed class NullOpenAiConnectionTester : IOpenAiConnectionTester
{
    public Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ConnectionTestResult(
            false,
            "API key is not configured."));
    }
}
