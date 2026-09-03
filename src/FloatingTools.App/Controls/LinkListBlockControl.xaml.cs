using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Direction;
using FloatingTools.App.SharedUi.Popups;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Controls;

public partial class LinkListBlockControl : UserControl
{
    private PopupAnchorService _popupAnchorService = null!;
    private LinkListItemActionsViewModel? _linkItemActions;
    private NoteLinkItem? _selectedItem;
    private FrameworkElement? _selectedElement;
    private NoteDirectionalTextBox? _editInput;
    private Button? _nameTab;
    private Button? _urlTab;
    private TextBlock? _editError;
    private string _draftName = string.Empty;
    private string _draftUrl = string.Empty;
    private LinkToolbarMode _toolbarMode;
    private bool _isReplacingPopup;

    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(
        nameof(Owner), typeof(NotesToolViewModel), typeof(LinkListBlockControl));

    public static readonly DependencyProperty ClampBoundsElementProperty = DependencyProperty.Register(
        nameof(ClampBoundsElement), typeof(FrameworkElement), typeof(LinkListBlockControl));

    public static readonly DependencyProperty PopupHostProperty = DependencyProperty.Register(
        nameof(PopupHost), typeof(AnchoredPopupHost), typeof(LinkListBlockControl));

    public NotesToolViewModel? Owner
    {
        get => (NotesToolViewModel?)GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }

    public FrameworkElement? ClampBoundsElement
    {
        get => (FrameworkElement?)GetValue(ClampBoundsElementProperty);
        set => SetValue(ClampBoundsElementProperty, value);
    }

    public AnchoredPopupHost? PopupHost
    {
        get => (AnchoredPopupHost?)GetValue(PopupHostProperty);
        set => SetValue(PopupHostProperty, value);
    }

    private LinkListNoteBlock? Block => DataContext as LinkListNoteBlock;

    internal LinkListItemActionsViewModel LinkItemActions => _linkItemActions ??=
        new LinkListItemActionsViewModel(
            () => Owner,
            () => Block,
            BeginEditingItem,
            CloseToolbar,
            FocusDraft);

    public LinkListBlockControl()
    {
        InitializePopupAnchorService(new PopupAnchorService(() =>
            PopupHost ?? throw new InvalidOperationException(
                "LinkList requires the connected Notes popup host.")));
    }

    internal LinkListBlockControl(PopupAnchorService popupAnchorService)
    {
        InitializePopupAnchorService(popupAnchorService);
    }

    private void InitializePopupAnchorService(PopupAnchorService popupAnchorService)
    {
        _popupAnchorService = popupAnchorService
            ?? throw new ArgumentNullException(nameof(popupAnchorService));
        InitializeComponent();
        _popupAnchorService.Closed += PopupAnchorService_OnClosed;
        Loaded += LinkListBlockControl_OnLoaded;
        Unloaded += LinkListBlockControl_OnUnloaded;
    }

    internal PopupAnchorService PopupAnchorService => _popupAnchorService;

    internal NoteDirectionalTextBox? ActiveEditInput => _editInput;

    public void FocusDraft() => Dispatcher.BeginInvoke(
        DispatcherPriority.Input, () => DraftToken.Focus());

    private void DraftToken_OnTextChanged(object sender, TextChangedEventArgs e) =>
        DraftToken.Width = Math.Clamp(24 + DraftToken.Text.Length * 7, 24, 240);

    private async void DraftToken_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control
            && e.Key == Key.V
            && Clipboard.ContainsText())
        {
            Commit(Clipboard.GetText());
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Space or Key.Tab or Key.Enter)
        {
            Commit(DraftToken.Text);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back
            && DraftToken.Text.Length == 0
            && Block is not null
            && Owner is not null)
        {
            await Owner.DeleteLastLinkItemOrBlockAsync(Block);
            e.Handled = true;
        }
    }

    private void Commit(string input)
    {
        if (Block is null || Owner is null)
        {
            return;
        }

        var result = Owner.CommitLinkTokens(Block, input);
        DraftToken.Text = result.InvalidDraft;
        DraftError.Visibility = string.IsNullOrEmpty(result.InvalidDraft)
            ? Visibility.Collapsed
            : Visibility.Visible;
        DraftToken.CaretIndex = DraftToken.Text.Length;
    }

    private void LinkItem_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteLinkItem item } element
            || Owner is null)
        {
            return;
        }

        HandleLinkActivation(item, element, e.ClickCount, Keyboard.Modifiers);

        e.Handled = true;
    }

    private void LinkItem_OnPreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e) => e.Handled = true;

    private void BlockContextMenu_OnOpened(object sender, RoutedEventArgs e) =>
        ApplySharedNotesMenuStyles();

    private void LinkItem_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteLinkItem item }
            || Owner is null
            || Block is null)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            LinkItemActions.CopyLinkItemCommand.Execute(item);
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Back)
        {
            LinkItemActions.DeleteLinkItemCommand.Execute(item);
            e.Handled = true;
        }
    }

    internal void Open_OnClick(object sender, RoutedEventArgs e)
        => ExecuteSelectedItem(LinkItemActions.OpenLinkItemCommand);

    internal Task OpenSelectedItemAsync() =>
        ExecuteSelectedItemAsync(LinkItemActions.OpenLinkItemCommand);

    internal void Copy_OnClick(object sender, RoutedEventArgs e)
        => ExecuteSelectedItem(LinkItemActions.CopyLinkItemCommand);

    internal void Edit_OnClick(object sender, RoutedEventArgs e)
        => ExecuteSelectedItem(LinkItemActions.EditLinkItemCommand);

    private void BeginEditingItem(NoteLinkItem item)
    {
        var element = _selectedElement;
        if (element is null || !ReferenceEquals(item, _selectedItem))
        {
            return;
        }

        _draftName = item.VisibleName;
        _draftUrl = item.Url;
        _toolbarMode = LinkToolbarMode.EditName;

        var content = CreatePopupContent("LinkEditToolbarTemplate");
        _nameTab = FindRequired<Button>(content, "NameTab");
        _urlTab = FindRequired<Button>(content, "UrlTab");
        _editInput = FindRequired<NoteDirectionalTextBox>(content, "EditInput");
        _editError = FindRequired<TextBlock>(content, "EditError");
        ShowEditValue(selectAll: true);

        ShowPopup(CreatePopupRequest(element, content));
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            _editInput?.Focus();
            _editInput?.SelectAll();
        });
    }

    internal void DeleteItem_OnClick(object sender, RoutedEventArgs e)
        => ExecuteSelectedItem(LinkItemActions.DeleteLinkItemCommand);

    internal Task DeleteSelectedItemAsync() =>
        ExecuteSelectedItemAsync(LinkItemActions.DeleteLinkItemCommand);

    internal void HandleLinkActivation(
        NoteLinkItem item,
        FrameworkElement element,
        int clickCount,
        ModifierKeys modifiers)
    {
        if (IsDirectOpenGesture(clickCount, modifiers))
        {
            LinkItemActions.OpenLinkItemCommand.Execute(item);
        }
        else
        {
            OpenActionToolbar(item, element);
        }
    }

    internal Task HandleLinkActivationAsync(
        NoteLinkItem item,
        FrameworkElement element,
        int clickCount,
        ModifierKeys modifiers)
    {
        if (IsDirectOpenGesture(clickCount, modifiers))
        {
            return LinkItemActions.OpenLinkItemCommand.ExecuteAsync(item);
        }

        OpenActionToolbar(item, element);
        return Task.CompletedTask;
    }

    internal void NameTab_OnClick(object sender, RoutedEventArgs e)
    {
        CaptureEditValue();
        _toolbarMode = LinkToolbarMode.EditName;
        ShowEditValue(selectAll: false);
        _editInput?.Focus();
    }

    internal void UrlTab_OnClick(object sender, RoutedEventArgs e)
    {
        CaptureEditValue();
        _toolbarMode = LinkToolbarMode.EditUrl;
        ShowEditValue(selectAll: false);
        _editInput?.Focus();
    }

    private void PopupContent_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CloseToolbar();
        e.Handled = true;
    }

    private async void EditInput_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseToolbar();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SaveEditAsync();
        }
    }

    internal async Task SaveEditAsync()
    {
        if (_selectedItem is null || Block is null || Owner is null)
        {
            return;
        }

        CaptureEditValue();
        var request = new LinkListItemEditRequest(
            Block,
            _selectedItem,
            _draftName,
            _draftUrl);
        await LinkItemActions.SaveEditedLinkItemCommand.ExecuteAsync(request);
        if (!LinkItemActions.LastEditSucceeded)
        {
            if (_editError is not null)
            {
                _editError.Visibility = Visibility.Visible;
            }

            return;
        }

        CloseToolbar();
    }

    private void ExecuteSelectedItem(IRelayCommand<NoteLinkItem> command)
    {
        if (_selectedItem is not null)
        {
            command.Execute(_selectedItem);
        }
    }

    private void ExecuteSelectedItem(IAsyncRelayCommand<NoteLinkItem> command)
    {
        if (_selectedItem is not null)
        {
            command.Execute(_selectedItem);
        }
    }

    private Task ExecuteSelectedItemAsync(IAsyncRelayCommand<NoteLinkItem> command) =>
        _selectedItem is null
            ? Task.CompletedTask
            : command.ExecuteAsync(_selectedItem);

    private static bool IsDirectOpenGesture(int clickCount, ModifierKeys modifiers) =>
        clickCount == 2 || modifiers.HasFlag(ModifierKeys.Control);

    private void CopyBlock_OnClick(object sender, RoutedEventArgs e)
    {
        if (Block is not null)
        {
            Owner?.CopyLinkListBlock(Block);
        }
    }

    private async void CutBlock_OnClick(object sender, RoutedEventArgs e)
    {
        if (Block is not null && Owner is not null)
        {
            await Owner.CutLinkListBlockAsync(Block);
        }
    }

    private async void DeleteBlock_OnClick(object sender, RoutedEventArgs e)
    {
        if (Block is not null && Owner is not null)
        {
            await Owner.DeleteLinkListBlockAsync(Block);
        }
    }

    internal void OpenActionToolbar(NoteLinkItem item, FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(element);

        _selectedItem = item;
        _selectedElement = element;
        _toolbarMode = LinkToolbarMode.Actions;
        var content = CreatePopupContent("LinkActionToolbarTemplate");
        ShowPopup(CreatePopupRequest(element, content));
    }

    private PopupAnchorRequest CreatePopupRequest(
        FrameworkElement target,
        FrameworkElement content)
    {
        var clampBoundsElement = ClampBoundsElement
            ?? throw new InvalidOperationException(
                "LinkList requires the Notes content surface as its popup clamp element.");
        var notesScrollViewer = FindAncestor<ScrollViewer>(this)
            ?? throw new InvalidOperationException(
                "LinkList requires an ancestor Notes ScrollViewer for popup close-on-scroll.");

        return new PopupAnchorRequest(target, content, clampBoundsElement)
        {
            PreferredPlacement = PopupAnchorPreferredPlacement.Below,
            CloseOnExternalClick = true,
            CloseOnScrollOf = notesScrollViewer,
            SingleInstance = true
        };
    }

    private FrameworkElement CreatePopupContent(string templateKey)
    {
        var template = (DataTemplate)FindResource(templateKey);
        var content = (FrameworkElement)template.LoadContent();
        ApplySharedNotesMenuStyles(content);
        return content;
    }

    private void ShowPopup(PopupAnchorRequest request)
    {
        _isReplacingPopup = true;
        try
        {
            _popupAnchorService.Show(request);
        }
        finally
        {
            _isReplacingPopup = false;
        }
    }

    private void CloseToolbar()
    {
        if (_popupAnchorService.IsOpen)
        {
            _popupAnchorService.Close();
        }
        else
        {
            ResetPopupState();
        }
    }

    private void PopupAnchorService_OnClosed(object? sender, EventArgs e)
    {
        if (!_isReplacingPopup)
        {
            ResetPopupState();
        }
    }

    private void ResetPopupState()
    {
        _toolbarMode = LinkToolbarMode.None;
        _selectedItem = null;
        _selectedElement = null;
        _editInput = null;
        _nameTab = null;
        _urlTab = null;
        _editError = null;
        _draftName = string.Empty;
        _draftUrl = string.Empty;
    }

    private void CaptureEditValue()
    {
        if (_editInput is null)
        {
            return;
        }

        if (_toolbarMode == LinkToolbarMode.EditName)
        {
            _draftName = _editInput.Text;
        }

        if (_toolbarMode == LinkToolbarMode.EditUrl)
        {
            _draftUrl = _editInput.Text;
        }
    }

    private void ShowEditValue(bool selectAll)
    {
        if (_editInput is null || _nameTab is null || _urlTab is null)
        {
            return;
        }

        var isUrl = _toolbarMode == LinkToolbarMode.EditUrl;
        _editInput.Text = isUrl ? _draftUrl : _draftName;
        if (isUrl)
        {
            // URLs are protocol input, not natural-language Notes content.
            _editInput.FlowDirection = FlowDirection.LeftToRight;
            _editInput.TextAlignment = TextAlignment.Left;
        }
        else
        {
            var resolution = TextDirectionResolver.Resolve(_draftName);
            _editInput.FlowDirection = resolution.Direction == TextDirection.RightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
            _editInput.TextAlignment = resolution.Alignment == TextDirectionAlignment.Right
                ? TextAlignment.Right
                : TextAlignment.Left;
        }
        _nameTab.Opacity = isUrl ? 0.62 : 1;
        _urlTab.Opacity = isUrl ? 1 : 0.62;
        if (selectAll)
        {
            _editInput.SelectAll();
        }
    }

    private void LinkListBlockControl_OnLoaded(object sender, RoutedEventArgs e) =>
        ApplySharedNotesMenuStyles();

    private void LinkListBlockControl_OnUnloaded(object sender, RoutedEventArgs e) =>
        CloseToolbar();

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (DependencyObject? current = source;
             current is not null;
             current = VisualTreeHelper.GetParent(current))
        {
            if (current is T result)
            {
                return result;
            }
        }

        return null;
    }

    private static T FindRequired<T>(FrameworkElement root, string name)
        where T : FrameworkElement =>
        root.FindName(name) as T
        ?? throw new InvalidOperationException(
            $"Popup template element '{name}' was not found.");

    private void ApplySharedNotesMenuStyles(FrameworkElement? popupSurface = null)
    {
        if (TryFindResource("NotesContextMenuStyle") is Style contextMenuStyle)
        {
            BlockContextMenu.Style = contextMenuStyle;
        }

        if (TryFindResource("NotesActionMenuItemStyle") is Style menuItemStyle)
        {
            foreach (var item in BlockContextMenu.Items.OfType<MenuItem>())
            {
                item.Style = menuItemStyle;
            }
        }

        if (popupSurface is not null
            && TryFindResource("NotesMenuSurfaceStyle") is Style menuSurfaceStyle)
        {
            popupSurface.Style = menuSurfaceStyle;
        }
    }

    private enum LinkToolbarMode
    {
        None,
        Actions,
        EditName,
        EditUrl
    }
}
