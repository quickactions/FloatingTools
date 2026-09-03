using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FloatingTools.Tests")]

namespace FloatingTools.App.SharedUi.Popups;

/// <summary>
/// Owns the active contextual popup and arbitrates single-instance requests.
/// Calling <see cref="Show"/> again replaces the service's current popup.
/// </summary>
public sealed class PopupAnchorService
{
    private static readonly object ArbitrationGate = new();
    private static WeakReference<PopupAnchorService>? _singleActiveService;

    private readonly Func<IPopupAnchorHost> _hostFactory;
    private IPopupAnchorHost? _activeHost;

    public PopupAnchorService()
        : this(() => new AnchoredPopupHost())
    {
    }

    internal PopupAnchorService(Func<IPopupAnchorHost> hostFactory)
    {
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
    }

    public bool IsOpen => _activeHost?.IsOpen == true;

    public PopupAnchorRequest? ActiveRequest => _activeHost?.Request;

    public event EventHandler? Closed;

    public void Show(PopupAnchorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Close();

        if (request.SingleInstance)
        {
            ClaimSingleInstance();
        }

        var host = _hostFactory();
        _activeHost = host;
        host.Closed += ActiveHost_OnClosed;
        try
        {
            host.Show(request);
        }
        catch
        {
            host.Closed -= ActiveHost_OnClosed;
            _activeHost = null;
            try
            {
                host.Close();
            }
            finally
            {
                ReleaseSingleInstance();
            }

            throw;
        }
    }

    public void Close()
    {
        var host = _activeHost;
        if (host is null)
        {
            ReleaseSingleInstance();
            return;
        }

        host.Closed -= ActiveHost_OnClosed;
        _activeHost = null;
        try
        {
            host.Close();
        }
        finally
        {
            ReleaseSingleInstance();
        }

        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void ClaimSingleInstance()
    {
        PopupAnchorService? previous = null;
        lock (ArbitrationGate)
        {
            if (_singleActiveService is not null
                && _singleActiveService.TryGetTarget(out var existing)
                && !ReferenceEquals(existing, this))
            {
                previous = existing;
            }

            _singleActiveService = new WeakReference<PopupAnchorService>(this);
        }

        previous?.Close();
    }

    private void ReleaseSingleInstance()
    {
        lock (ArbitrationGate)
        {
            if (_singleActiveService is not null
                && _singleActiveService.TryGetTarget(out var active)
                && ReferenceEquals(active, this))
            {
                _singleActiveService = null;
            }
        }
    }

    private void ActiveHost_OnClosed(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _activeHost))
        {
            return;
        }

        _activeHost!.Closed -= ActiveHost_OnClosed;
        _activeHost = null;
        ReleaseSingleInstance();
        Closed?.Invoke(this, EventArgs.Empty);
    }
}

internal interface IPopupAnchorHost
{
    event EventHandler? Closed;

    bool IsOpen { get; }

    PopupAnchorRequest? Request { get; }

    void Show(PopupAnchorRequest request);

    void Close();
}
