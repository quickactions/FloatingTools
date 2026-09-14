using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace FloatingTools.Tests;

/// <summary>
/// Runs resource-backed WPF integration tests on one long-lived STA. WPF permits
/// only one Application per AppDomain, so creating one Application per test makes
/// the full suite order-dependent and can accidentally start the production App.
/// </summary>
internal static class WpfTestApplication
{
    private static readonly Lazy<Dispatcher> AppDispatcher = new(CreateDispatcher);

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var operation = AppDispatcher.Value.InvokeAsync(action);
        if (!operation.Task.Wait(TimeSpan.FromSeconds(20)))
        {
            throw new TimeoutException("The shared WPF test dispatcher did not complete in time.");
        }

        operation.Task.GetAwaiter().GetResult();
    }

    private static Dispatcher CreateDispatcher()
    {
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                LoadResources(application);
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
                GC.KeepAlive(application);
            }
            catch (Exception exception)
            {
                failure = exception;
                ready.Set();
            }
        })
        {
            IsBackground = true,
            Name = "FloatingTools WPF test dispatcher"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(20)));
        Assert.Null(failure);
        return dispatcher ?? throw new InvalidOperationException(
            "The shared WPF test dispatcher did not initialize.");
    }

    private static void LoadResources(Application application)
    {
        foreach (var source in new[]
        {
            "SharedUi/Tokens/Colors.Dark.xaml",
            "SharedUi/Tokens/Typography.xaml",
            "SharedUi/Tokens/Spacing.xaml",
            "SharedUi/Tokens/Radii.xaml",
            "SharedUi/Styles/Buttons.xaml",
            "SharedUi/Styles/ContextMenus.xaml",
            "SharedUi/Styles/ScrollBars.xaml",
            "SharedUi/Styles/SettingsPageShell.xaml",
            "SharedUi/Styles/SettingsComboBoxes.xaml",
            "SharedUi/Styles/TextBoxes.xaml",
            "SharedUi/Styles/Inputs.xaml",
            "SharedUi/Styles/ToolHeader.xaml",
            "SharedUi/Styles/Tooltips.xaml",
            "Resources/SharedWindowStyles.xaml"
        })
        {
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/FloatingTools.App;component/{source}",
                    UriKind.Absolute)
            });
        }
    }
}
