namespace FloatingTools.App.Services;

internal sealed record ExitPreparationStep(
    string Name,
    Func<Task> ExecuteAsync);

internal sealed class ExitWorkflow
{
    private readonly IReadOnlyList<ExitPreparationStep> _preparationSteps;
    private readonly Action _closeWindows;
    private readonly Action _shutdownApplication;
    private readonly Action<string, Exception> _reportFailure;
    private readonly object _gate = new();
    private Task? _executionTask;

    public ExitWorkflow(
        IReadOnlyList<ExitPreparationStep> preparationSteps,
        Action closeWindows,
        Action shutdownApplication,
        Action<string, Exception> reportFailure)
    {
        _preparationSteps = preparationSteps
            ?? throw new ArgumentNullException(nameof(preparationSteps));
        _closeWindows = closeWindows
            ?? throw new ArgumentNullException(nameof(closeWindows));
        _shutdownApplication = shutdownApplication
            ?? throw new ArgumentNullException(nameof(shutdownApplication));
        _reportFailure = reportFailure
            ?? throw new ArgumentNullException(nameof(reportFailure));
    }

    public Task ExecuteAsync()
    {
        lock (_gate)
        {
            return _executionTask ??= ExecuteCoreAsync();
        }
    }

    private async Task ExecuteCoreAsync()
    {
        foreach (var step in _preparationSteps)
        {
            try
            {
                await step.ExecuteAsync();
            }
            catch (Exception exception)
            {
                _reportFailure(step.Name, exception);
            }
        }

        try
        {
            _closeWindows();
        }
        catch (Exception exception)
        {
            _reportFailure("window close", exception);
        }

        try
        {
            _shutdownApplication();
        }
        catch (Exception exception)
        {
            _reportFailure("application shutdown", exception);
        }
    }
}
