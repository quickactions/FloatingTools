using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public interface IAppSettingsStore
{
    AppSettings Load();

    bool Save(AppSettings settings);
}
