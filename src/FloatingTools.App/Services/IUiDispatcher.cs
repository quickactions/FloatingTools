namespace FloatingTools.App.Services;

public interface IUiDispatcher
{
    bool CheckAccess();

    void Invoke(Action action);
}

public sealed class SynchronizationContextUiDispatcher : IUiDispatcher
{
    private readonly SynchronizationContext? _context;
    private readonly int _ownerThreadId;

    public SynchronizationContextUiDispatcher(
        SynchronizationContext? context = null)
    {
        _context = context ?? SynchronizationContext.Current;
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    public bool CheckAccess() =>
        Environment.CurrentManagedThreadId == _ownerThreadId;

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (CheckAccess() || _context is null)
        {
            action();
            return;
        }

        _context.Send(_ => action(), null);
    }
}
