using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Views;

public partial class TranslationToolView : UserControl
{
    private TranslationToolViewModel? _viewModel;

    public TranslationToolView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeToItems(DataContext as TranslationToolViewModel);
    }

    private void OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeFromItems();
        SubscribeToItems(e.NewValue as TranslationToolViewModel);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () =>
            {
                FeedScrollViewer.ScrollToEnd();
                ComposerTextBox.Focus();
            });
    }

    private void ComposerTextBox_OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return;
        }

        e.Handled = true;
        if (_viewModel?.SendCommand.CanExecute(null) == true)
        {
            _viewModel.SendCommand.Execute(null);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeFromItems();
    }

    private void SaveApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.Settings.SaveApiKey(SettingsApiKeyPasswordBox.Password)
            == true)
        {
            SettingsApiKeyPasswordBox.Clear();
        }
    }

    private void CancelApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.Settings.CancelApiKeyEditCommand.Execute(null);
        SettingsApiKeyPasswordBox.Clear();
    }

    private void SettingsApiKeyPasswordBox_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            SettingsApiKeyPasswordBox.Clear();
        }
    }

    private void UnsubscribeFromItems()
    {
        if (_viewModel is not null)
        {
            _viewModel.Items.CollectionChanged -= OnItemsChanged;
            _viewModel.ComposerFocusRequested -= OnComposerFocusRequested;
            _viewModel = null;
        }
    }

    private void SubscribeToItems(TranslationToolViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.Items.CollectionChanged += OnItemsChanged;
        _viewModel.ComposerFocusRequested += OnComposerFocusRequested;
    }

    private void OnComposerFocusRequested(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                ComposerTextBox.Focus();
                ComposerTextBox.CaretIndex = ComposerTextBox.Text.Length;
            });
    }

    public void FocusSearch()
    {
        _viewModel?.OpenSavedWordsCommand.Execute(null);
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                SavedWordsSearchTextBox.Focus();
                SavedWordsSearchTextBox.SelectAll();
            });
    }
}
