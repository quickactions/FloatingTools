using System.Diagnostics;
using FloatingTools.App.Services;

namespace FloatingTools.App.Platform.Windows;

public sealed class WindowsNoteLinkLauncher : INoteLinkLauncher
{
    public async Task<bool> TryOpenAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!LinkUrlValidator.TryValidate(url, out var safeUrl)) return false;
        try
        {
            await Task.Run(
                () => Process.Start(new ProcessStartInfo(safeUrl) { UseShellExecute = true }),
                cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
