using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class ComboBoxScrollDismissBehaviorTests
{
    [Fact]
    public void OwningScrollViewerScroll_ClosesDropDownAndPreservesSelection()
    {
        RunOnSta(() =>
        {
            var comboBox = new ComboBox
            {
                ItemsSource = new[] { "Sol", "Terra", "Luna" },
                SelectedIndex = 1
            };
            ComboBoxScrollDismissBehavior.SetIsEnabled(comboBox, true);

            var content = new StackPanel();
            content.Children.Add(comboBox);
            content.Children.Add(new Border { Height = 800 });
            var scrollViewer = new ScrollViewer
            {
                Height = 120,
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var window = new Window
            {
                Width = 240,
                Height = 160,
                ShowInTaskbar = false,
                Content = scrollViewer
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                var selectedItem = comboBox.SelectedItem;
                comboBox.IsDropDownOpen = true;

                scrollViewer.ScrollToVerticalOffset(80);
                window.Dispatcher.Invoke(
                    DispatcherPriority.Background,
                    static () => { });

                Assert.False(comboBox.IsDropDownOpen);
                Assert.Same(selectedItem, comboBox.SelectedItem);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OuterScrollViewerScroll_ClosesDropDownInsideNestedViewer()
    {
        RunOnSta(() =>
        {
            var comboBox = new ComboBox
            {
                ItemsSource = new[] { "Automatic", "English", "Hebrew" },
                SelectedIndex = 0
            };
            ComboBoxScrollDismissBehavior.SetIsEnabled(comboBox, true);

            var innerContent = new StackPanel();
            innerContent.Children.Add(comboBox);
            innerContent.Children.Add(new Border { Height = 200 });
            var innerViewer = new ScrollViewer
            {
                Height = 220,
                Content = innerContent
            };
            var outerContent = new StackPanel();
            outerContent.Children.Add(innerViewer);
            outerContent.Children.Add(new Border { Height = 600 });
            var outerViewer = new ScrollViewer
            {
                Height = 120,
                Content = outerContent
            };
            var window = new Window
            {
                Width = 240,
                Height = 160,
                ShowInTaskbar = false,
                Content = outerViewer
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                comboBox.IsDropDownOpen = true;

                outerViewer.ScrollToVerticalOffset(80);
                window.Dispatcher.Invoke(
                    DispatcherPriority.Background,
                    static () => { });

                Assert.False(comboBox.IsDropDownOpen);
                Assert.Equal("Automatic", comboBox.SelectedItem);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SharedSettingsStyle_EnablesOnlyTheAffectedComboBoxes()
    {
        var xaml = File.ReadAllText(FindSharedStylePath());

        Assert.Contains(
            "<Setter Property=\"controls:ComboBoxScrollDismissBehavior.IsEnabled\" Value=\"True\" />",
            xaml);
        Assert.Contains("x:Key=\"SettingsComboBoxStyle\"", xaml);
        Assert.Contains("ContentTemplate=\"{Binding SelectionBoxItemTemplate,", xaml);
        Assert.Contains("<ItemsPresenter />", xaml);
    }

    private static void RunOnSta(Action action)
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
            throw failure;
        }
    }

    private static string FindSharedStylePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "FloatingTools.App",
                    "SharedUi",
                    "Styles",
                    "SettingsComboBoxes.xaml");
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
