namespace FloatingTools.App.Services.OpenAI;

public interface IOpenAiConnectionTester
{
    Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken);
}

public sealed record ConnectionTestResult(bool IsSuccess, string Message);
