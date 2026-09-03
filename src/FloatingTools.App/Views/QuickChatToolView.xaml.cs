using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using FloatingTools.App.Platform.Windows;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;
using Microsoft.Win32;

namespace FloatingTools.App.Views;

public partial class QuickChatToolView : UserControl
{
    private QuickChatViewModel? _viewModel;
    private bool _followOutput = true;
    private Task? _initializationTask;

    public QuickChatToolView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe(DataContext as QuickChatViewModel);
        if (IsVisible)
        {
            await EnsureInitializedAsync();
        }

        FollowOutput();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Unsubscribe();

    private async void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Subscribe(DataContext as QuickChatViewModel);
            await EnsureInitializedAsync();
            FollowOutput();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_viewModel is null || _viewModel.IsInitialized)
        {
            return;
        }

        _initializationTask ??= _viewModel.InitializeAsync();
        try
        {
            await _initializationTask;
        }
        finally
        {
            _initializationTask = null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Unsubscribe();
        if (IsLoaded)
        {
            Subscribe(e.NewValue as QuickChatViewModel);
        }
    }

    private void Subscribe(QuickChatViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.ScrollFollowRequested += OnScrollFollowRequested;
    }

    private void Unsubscribe()
    {
        if (_viewModel is not null)
        {
            _viewModel.ScrollFollowRequested -= OnScrollFollowRequested;
            _viewModel = null;
        }
    }

    private void OnScrollFollowRequested(object? sender, EventArgs e)
    {
        if (_followOutput)
        {
            FollowOutput();
        }
    }

    private void FollowOutput() => Dispatcher.BeginInvoke(
        DispatcherPriority.Loaded,
        () => ConversationScrollViewer.ScrollToEnd());

    private void ConversationScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _followOutput = ConversationScrollViewer.ScrollableHeight
            - ConversationScrollViewer.VerticalOffset <= 36;
    }

    private void ComposerTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V
            && Keyboard.Modifiers == ModifierKeys.Control
            && ClipboardContainsImage()
            && _viewModel?.AttachClipboardImageCommand.CanExecute(null) == true)
        {
            _viewModel.AttachClipboardImageCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter
            || (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return;
        }

        e.Handled = true;
        if (_viewModel?.SendCommand.CanExecute(null) == true)
        {
            _viewModel.SendCommand.Execute(null);
        }
    }

    private static bool ClipboardContainsImage()
    {
        try
        {
            return Clipboard.ContainsImage();
        }
        catch (COMException)
        {
            return false;
        }
    }

    private void AttachImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Attach image",
            Filter = "PNG and JPEG images|*.png;*.jpg;*.jpeg|PNG images|*.png|JPEG images|*.jpg;*.jpeg",
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true
            && _viewModel?.AttachImageFileCommand.CanExecute(dialog.FileName) == true)
        {
            _viewModel.AttachImageFileCommand.Execute(dialog.FileName);
        }
    }

    private void ComposerContainer_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        var hasSupportedImage = _viewModel?.CanAttachImage == true
            && GetDroppedFiles(e.Data).Any(QuickChatImageDropPolicy.IsSupported);
        e.Effects = hasSupportedImage ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void ComposerContainer_OnPreviewDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_viewModel is null)
        {
            return;
        }

        var availableCapacity = QuickChatViewModel.MaximumPendingAttachmentCount
            - _viewModel.PendingAttachments.Count;
        var files = QuickChatImageDropPolicy.SelectSupported(
            GetDroppedFiles(e.Data),
            availableCapacity);
        foreach (var file in files)
        {
            if (!_viewModel.CanAttachImage)
            {
                break;
            }

            try
            {
                await _viewModel.AttachImageFileAsync(file);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException)
            {
                Debug.WriteLine($"Quick Chat rejected dropped image: {exception.GetType().Name}");
            }
        }
    }

    private static IReadOnlyList<string> GetDroppedFiles(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop)
            && data.GetData(DataFormats.FileDrop) is string[] files
                ? files
                : [];

    private void AttachmentImage_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var path = (sender as FrameworkElement)?.DataContext switch
        {
            QuickChatPendingAttachmentViewModel pending => pending.RuntimePath,
            QuickChatAttachmentPresentation attachment => attachment.RuntimePath,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        OpenImageViewer(path);
        e.Handled = true;
    }

    private void OpenImageViewer(string imagePath)
    {
        var owner = Window.GetWindow(this);
        var placementService = new WindowPlacementService();
        var monitors = placementService.GetMonitors();
        var handle = owner is null
            ? IntPtr.Zero
            : new System.Windows.Interop.WindowInteropHelper(owner).Handle;
        var monitor = handle != IntPtr.Zero
            ? WindowPlacementCalculator.SelectMonitor(
                placementService.GetWindowBounds(handle),
                monitors)
            : monitors.First(item => item.IsPrimary);
        var viewer = new ImageViewerWindow(imagePath, monitor);
        if (owner is not null)
        {
            viewer.Owner = owner;
        }

        viewer.ShowDialog();
    }

    private void SaveApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.Settings?.SaveApiKey(QuickChatApiKeyPasswordBox.Password) == true)
        {
            QuickChatApiKeyPasswordBox.Clear();
        }
    }

    private void CancelApiKey_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.Settings?.CancelApiKeyEditCommand.Execute(null);
        QuickChatApiKeyPasswordBox.Clear();
    }

    private void QuickChatApiKeyPasswordBox_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            QuickChatApiKeyPasswordBox.Clear();
        }
    }
}
