using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FloatingTools.App.Models;

public sealed class LinkListNoteBlock : NoteBlock
{
    private ObservableCollection<NoteLinkItem> _items = [];

    public LinkListNoteBlock() => Subscribe(_items);

    public ObservableCollection<NoteLinkItem> Items
    {
        get => _items;
        set
        {
            if (ReferenceEquals(_items, value)) return;
            Unsubscribe(_items);
            _items = value ?? [];
            Subscribe(_items);
            OnPropertyChanged();
        }
    }

    private void Subscribe(ObservableCollection<NoteLinkItem> items)
    {
        items.CollectionChanged += OnItemsChanged;
        foreach (var item in items) item.PropertyChanged += OnItemChanged;
    }

    private void Unsubscribe(ObservableCollection<NoteLinkItem> items)
    {
        items.CollectionChanged -= OnItemsChanged;
        foreach (var item in items) item.PropertyChanged -= OnItemChanged;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (NoteLinkItem item in e.OldItems) item.PropertyChanged -= OnItemChanged;
        if (e.NewItems is not null)
            foreach (NoteLinkItem item in e.NewItems) item.PropertyChanged += OnItemChanged;
        OnPropertyChanged(nameof(Items));
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(Items));
}

public partial class NoteLinkItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    public string VisibleName => string.IsNullOrWhiteSpace(DisplayName) ? Url : DisplayName;

    partial void OnUrlChanged(string value) => OnPropertyChanged(nameof(VisibleName));
    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(VisibleName));
}
