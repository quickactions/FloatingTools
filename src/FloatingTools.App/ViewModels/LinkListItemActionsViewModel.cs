using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.ViewModels;

/// <summary>
/// Owns LinkList item actions while leaving WPF anchor lookup and popup geometry
/// in the LinkList control.
/// </summary>
public sealed class LinkListItemActionsViewModel
{
    private readonly Func<NotesToolViewModel?> _ownerProvider;
    private readonly Func<LinkListNoteBlock?> _blockProvider;
    private readonly Action<NoteLinkItem> _editRequested;
    private readonly Action _closeContextualUi;
    private readonly Action _focusDraft;

    public LinkListItemActionsViewModel(
        Func<NotesToolViewModel?> ownerProvider,
        Func<LinkListNoteBlock?> blockProvider,
        Action<NoteLinkItem> editRequested,
        Action closeContextualUi,
        Action focusDraft)
    {
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        _blockProvider = blockProvider ?? throw new ArgumentNullException(nameof(blockProvider));
        _editRequested = editRequested ?? throw new ArgumentNullException(nameof(editRequested));
        _closeContextualUi = closeContextualUi
            ?? throw new ArgumentNullException(nameof(closeContextualUi));
        _focusDraft = focusDraft ?? throw new ArgumentNullException(nameof(focusDraft));

        OpenLinkItemCommand = new AsyncRelayCommand<NoteLinkItem>(
            OpenLinkItemAsync,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CopyLinkItemCommand = new RelayCommand<NoteLinkItem>(CopyLinkItem);
        EditLinkItemCommand = new RelayCommand<NoteLinkItem>(RequestEditLinkItem);
        SaveEditedLinkItemCommand = new AsyncRelayCommand<LinkListItemEditRequest>(
            SaveEditedLinkItemAsync,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        DeleteLinkItemCommand = new AsyncRelayCommand<NoteLinkItem>(
            DeleteLinkItemAsync,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }

    public IAsyncRelayCommand<NoteLinkItem> OpenLinkItemCommand { get; }

    public IRelayCommand<NoteLinkItem> CopyLinkItemCommand { get; }

    public IRelayCommand<NoteLinkItem> EditLinkItemCommand { get; }

    public IAsyncRelayCommand<LinkListItemEditRequest> SaveEditedLinkItemCommand { get; }

    public IAsyncRelayCommand<NoteLinkItem> DeleteLinkItemCommand { get; }

    public bool LastEditSucceeded { get; private set; }

    private async Task OpenLinkItemAsync(NoteLinkItem? item)
    {
        if (item is null)
        {
            return;
        }

        _closeContextualUi();
        if (!LinkUrlValidator.TryValidate(item.Url, out _))
        {
            return;
        }

        var owner = _ownerProvider();
        if (owner is not null)
        {
            await owner.OpenLinkItemAsync(item);
        }
    }

    private void CopyLinkItem(NoteLinkItem? item)
    {
        if (item is null)
        {
            return;
        }

        _closeContextualUi();
        _ownerProvider()?.CopyLinkItem(item);
    }

    private void RequestEditLinkItem(NoteLinkItem? item)
    {
        if (item is not null)
        {
            _editRequested(item);
        }
    }

    private async Task SaveEditedLinkItemAsync(LinkListItemEditRequest? request)
    {
        LastEditSucceeded = false;
        if (request is null)
        {
            return;
        }

        var owner = _ownerProvider();
        var block = _blockProvider();
        if (owner is null
            || block is null
            || !ReferenceEquals(block, request.Block))
        {
            return;
        }

        LastEditSucceeded = await owner.EditLinkItemAsync(
            block,
            request.Item,
            request.DisplayName,
            request.Url);
    }

    private async Task DeleteLinkItemAsync(NoteLinkItem? item)
    {
        if (item is null)
        {
            return;
        }

        _closeContextualUi();
        var owner = _ownerProvider();
        var block = _blockProvider();
        if (owner is null || block is null)
        {
            return;
        }

        await owner.DeleteLinkItemAsync(block, item);
        _focusDraft();
    }
}

public sealed record LinkListItemEditRequest(
    LinkListNoteBlock Block,
    NoteLinkItem Item,
    string DisplayName,
    string Url);
