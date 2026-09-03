using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using FloatingTools.App.SharedUi.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class ToolHeaderControlSecondaryActionTests
{
    [Fact]
    public void SecondaryAction_DefaultsToAbsentAndReservesNoSpace()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();

            Assert.False(fixture.Header.IsSecondaryActionVisible);
            Assert.Null(fixture.Header.SecondaryActionContent);
            Assert.Null(fixture.Header.SecondaryActionCommand);
            Assert.Equal(Visibility.Collapsed, fixture.SecondaryAction.Visibility);
            Assert.Equal(0, fixture.SecondaryAction.ActualWidth);
        });
    }

    [Fact]
    public void SecondaryAction_IsVisibleOnlyWhileHeaderIsCollapsed()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            fixture.Header.SecondaryActionContent = "+";
            fixture.Header.SecondaryActionCommand = new CountingCommand();
            fixture.Header.IsSecondaryActionVisible = true;
            fixture.UpdateLayout();

            Assert.Equal(Visibility.Visible, fixture.SecondaryAction.Visibility);

            fixture.Header.IsMenuOpen = true;
            fixture.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, fixture.SecondaryAction.Visibility);

            fixture.Header.IsMenuOpen = false;
            fixture.UpdateLayout();
            Assert.Equal(Visibility.Visible, fixture.SecondaryAction.Visibility);
        });
    }

    [Fact]
    public void SecondaryAction_InvokesOnlyItsCommandAndDoesNotToggleHeader()
    {
        RunInSta(() =>
        {
            using var fixture = new HeaderFixture();
            var command = new CountingCommand();
            var headerToggleCount = 0;
            fixture.Header.Click += (_, _) =>
            {
                headerToggleCount++;
                fixture.Header.IsMenuOpen = !fixture.Header.IsMenuOpen;
            };
            fixture.Header.SecondaryActionContent = "+";
            fixture.Header.SecondaryActionCommand = command;
            fixture.Header.IsSecondaryActionVisible = true;
            fixture.UpdateLayout();

            var peer = new ButtonAutomationPeer(fixture.SecondaryAction);
            var invoke = Assert.IsAssignableFrom<IInvokeProvider>(
                peer.GetPattern(PatternInterface.Invoke));
            invoke.Invoke();
            fixture.Window.Dispatcher.Invoke(
                DispatcherPriority.Background,
                static () => { });

            Assert.Equal(1, command.InvocationCount);
            Assert.Equal(0, headerToggleCount);
            Assert.False(fixture.Header.IsMenuOpen);

            fixture.Header.RaiseEvent(
                new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(1, headerToggleCount);
            Assert.True(fixture.Header.IsMenuOpen);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }

    private sealed class HeaderFixture : IDisposable
    {
        public HeaderFixture()
        {
            Window = new Window
            {
                Width = 300,
                Height = 60,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };
            Header = new ToolHeaderControl
            {
                Width = 280,
                Height = 34,
                Title = "Header",
                Template = CreateSecondaryActionTemplate()
            };
            Window.Content = Header;
            Window.Show();
            UpdateLayout();
            SecondaryAction = Assert.IsType<Button>(
                Header.Template.FindName("PART_SecondaryAction", Header));
        }

        public Window Window { get; }

        public ToolHeaderControl Header { get; }

        public Button SecondaryAction { get; }

        public void UpdateLayout()
        {
            Header.ApplyTemplate();
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();

        private static ControlTemplate CreateSecondaryActionTemplate() =>
            (ControlTemplate)XamlReader.Parse(
                """
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                 xmlns:controls="clr-namespace:FloatingTools.App.SharedUi.Controls;assembly=FloatingTools.App"
                                 TargetType="{x:Type controls:ToolHeaderControl}">
                    <Grid>
                        <Button x:Name="PART_SecondaryAction"
                                Width="30"
                                Height="30"
                                Command="{Binding SecondaryActionCommand, RelativeSource={RelativeSource TemplatedParent}}"
                                Content="{TemplateBinding SecondaryActionContent}">
                            <Button.Style>
                                <Style TargetType="{x:Type Button}">
                                    <Setter Property="Visibility" Value="Collapsed" />
                                    <Style.Triggers>
                                        <MultiDataTrigger>
                                            <MultiDataTrigger.Conditions>
                                                <Condition Binding="{Binding IsSecondaryActionVisible, RelativeSource={RelativeSource TemplatedParent}}"
                                                           Value="True" />
                                                <Condition Binding="{Binding IsMenuOpen, RelativeSource={RelativeSource TemplatedParent}}"
                                                           Value="False" />
                                            </MultiDataTrigger.Conditions>
                                            <Setter Property="Visibility" Value="Visible" />
                                        </MultiDataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Button.Style>
                        </Button>
                    </Grid>
                </ControlTemplate>
                """);
    }

    private sealed class CountingCommand : ICommand
    {
        public int InvocationCount { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => InvocationCount++;
    }
}
