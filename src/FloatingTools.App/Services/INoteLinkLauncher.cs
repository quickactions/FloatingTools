namespace FloatingTools.App.Services;

public interface INoteLinkLauncher
{
    Task<bool> TryOpenAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class NullNoteLinkLauncher : INoteLinkLauncher
{
    public Task<bool> TryOpenAsync(string url, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
