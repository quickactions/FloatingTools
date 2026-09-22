using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class ToolbarTransientStatusRuntimeTests
{
    [Fact]
    public void ActiveStatus_IsDismissedWhenToolbarDragBegins()
        => WpfTestApplication.Run(() =>
        {
            using var fixture = ToolbarFixture.Create();
            var dragStarted = 0;
            fixture.Toolbar.DragStarted += (_, _) => dragStarted++;
            fixture.Toolbar.ShowTransientStatus("No text detected.");

            Invoke(fixture.Toolbar, "BeginToolbarDrag");

            Assert.False(fixture.Toolbar.IsTransientStatusVisible);
            Assert.Equal(1, dragStarted);
        });

    [Fact]
    public void ActiveStatus_AutomaticallyDismissesAfterNormalTimeout()
        => WpfTestApplication.Run(() =>
        {
            using var fixture = ToolbarFixture.Create();
            fixture.Toolbar.ShowTransientStatus("No text detected.");
            Assert.True(fixture.Toolbar.IsTransientStatusVisible);

            PumpDispatcher(TimeSpan.FromMilliseconds(3250));

            Assert.False(fixture.Toolbar.IsTransientStatusVisible);
        });

    [Fact]
    public void OrdinaryMouseMovementAndClick_DoNotDismissActiveStatus()
        => WpfTestApplication.Run(() =>
        {
            using var fixture = ToolbarFixture.Create();
            fixture.Toolbar.ShowTransientStatus("No text detected.");

            Invoke(
                fixture.Toolbar,
                "MainTileButton_OnPreviewMouseMove",
                fixture.Toolbar,
                new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount));
            Invoke(
                fixture.Toolbar,
                "MainTileButton_OnClick",
                fixture.Toolbar,
                new RoutedEventArgs());

            Assert.True(fixture.Toolbar.IsTransientStatusVisible);
        });

    [Fact]
    public void StaleTimeout_DoesNotDismissNewerStatus()
        => WpfTestApplication.Run(() =>
        {
            using var fixture = ToolbarFixture.Create();
            fixture.Toolbar.ShowTransientStatus("First");
            var staleGeneration = GetGeneration(fixture.Toolbar);

            fixture.Toolbar.ShowTransientStatus("Second");
            Invoke(fixture.Toolbar, "DismissTransientStatus", staleGeneration);

            Assert.True(fixture.Toolbar.IsTransientStatusVisible);
            Assert.Equal("Second", fixture.Toolbar.TransientStatusText);
        });

    private static long GetGeneration(ToolbarWindow toolbar)
        => (long)typeof(ToolbarWindow)
            .GetField("_transientStatusGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(toolbar)!;

    private static void Invoke(ToolbarWindow toolbar, string methodName, params object?[] arguments)
    {
        var parameterTypes = arguments.Select(argument => argument?.GetType()).ToArray();
        var method = typeof(ToolbarWindow)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate =>
            {
                if (candidate.Name != methodName) return false;
                var parameters = candidate.GetParameters();
                if (parameters.Length != parameterTypes.Length) return false;
                return parameters.Zip(parameterTypes).All(pair =>
                    pair.Second is null || pair.First.ParameterType.IsAssignableFrom(pair.Second));
            });
        method.Invoke(toolbar, arguments);
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class ToolbarFixture : IDisposable
    {
        private readonly string _settingsPath;

        private ToolbarFixture(ToolbarWindow toolbar, string settingsPath)
        {
            Toolbar = toolbar;
            _settingsPath = settingsPath;
        }

        public ToolbarWindow Toolbar { get; }

        public static ToolbarFixture Create()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
            var settings = new AppSettings();
            var toolbar = new ToolbarWindow(
                new WindowPlacementService(),
                new SettingsService(path),
                settings);
            toolbar.Show();
            toolbar.UpdateLayout();
            return new ToolbarFixture(toolbar, path);
        }

        public void Dispose()
        {
            Toolbar.CompletePendingDrag();
            if (Toolbar.IsLoaded)
            {
                Toolbar.Close();
            }

            File.Delete(_settingsPath);
        }
    }
}
