using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Views;

public partial class ApplicationSettingsView : UserControl
{
    public ApplicationSettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    private void SaveApplicationApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SaveApplicationApiKey(ApplicationApiKeyPasswordBox.Password)
            == true)
        {
            ApplicationApiKeyPasswordBox.Clear();
        }
    }

    private void CancelApplicationApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.CancelApplicationApiKeyEditCommand.Execute(null);
        ApplicationApiKeyPasswordBox.Clear();
    }

    private void ApplicationApiKeyPasswordBox_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            ApplicationApiKeyPasswordBox.Clear();
        }
    }

    private void ApplicationSettingsView_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            ViewModel?.CancelApplicationApiKeyEditCommand.Execute(null);
            ApplicationApiKeyPasswordBox.Clear();
        }
    }
}
