using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public sealed class InMemoryAppSettingsStore(AppSettings? initial = null)
    : IAppSettingsStore
{
    private AppSettings _settings = initial ?? new AppSettings();

    public AppSettings Load() => _settings;

    public bool Save(AppSettings settings)
    {
        _settings = settings;
        return true;
    }
}
