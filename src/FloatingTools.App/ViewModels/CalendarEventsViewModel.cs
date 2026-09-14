using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.ViewModels;

public partial class CalendarEventsViewModel : ObservableObject
{
    private IReadOnlyList<CalendarEntry> _sourceEntries;
    private readonly CalendarEventsQueryService _queryService;
    private readonly CalendarEventPreviewGenerator _previewGenerator;
    private readonly Action<Guid, DateOnly> _activateEntry;
    private bool _isHebrew;
    private readonly Func<DateOnly> _todayProvider;
    private readonly Func<DateOnly, DateOnly> _weekStartProvider;
    private CalendarLayoutMode _layoutMode;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CalendarEventsPreset _selectedPreset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortLabel))]
    [NotifyPropertyChangedFor(nameof(SortGlyph))]
    private bool _isDescending;

    public CalendarEventsViewModel(
        CalendarEventsMode mode,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        IEnumerable<CalendarEntry> entries,
        bool isHebrew,
        Action<Guid, DateOnly> activateEntry,
        CalendarEventsQueryService? queryService = null,
        CalendarEventPreviewGenerator? previewGenerator = null,
        Func<DateOnly>? todayProvider = null,
        Func<DateOnly, DateOnly>? weekStartProvider = null,
        CalendarLayoutMode layoutMode = CalendarLayoutMode.Compact)
    {
        Mode = mode;
        _todayProvider = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
        _weekStartProvider = weekStartProvider ?? (date => date.AddDays(
            -(((int)date.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek + 7) % 7)));
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
        _sourceEntries = entries.ToArray();
        _isHebrew = isHebrew;
        _activateEntry = activateEntry ?? throw new ArgumentNullException(nameof(activateEntry));
        _queryService = queryService ?? new CalendarEventsQueryService();
        _previewGenerator = previewGenerator ?? new CalendarEventPreviewGenerator();
        _layoutMode = layoutMode;
        RefreshItems();
    }

    public CalendarEventsMode Mode { get; }
    public CalendarLayoutMode LayoutMode => _layoutMode;
    public string SortGlyph => IsDescending ? "↓" : "↑";
    public DateOnly RangeStart { get; private set; }
    public DateOnly RangeEnd { get; private set; }
    public string SearchLabel => _isHebrew ? "חיפוש אירועים" : "Search events";
    public string CloseSearchLabel => _isHebrew ? "סגירת חיפוש" : "Close search";
    public string FilterLabel => _isHebrew ? "סינון לפי" : "Filter by";
    public IReadOnlyList<CalendarSettingChoice<CalendarEventsPreset>> PresetChoices =>
    [
        new(CalendarEventsPreset.All, _isHebrew ? "הכל" : "All"),
        new(CalendarEventsPreset.FromToday, _isHebrew ? "מהיום" : "From today"),
        new(CalendarEventsPreset.CurrentWeek, _isHebrew ? "השבוע הנוכחי" : "Current week"),
        new(CalendarEventsPreset.CurrentMonth, _isHebrew ? "החודש הנוכחי" : "Current month")
    ];
    public IReadOnlyList<CalendarEventListItemViewModel> Items { get; private set; } = [];
    public string SortLabel => _isHebrew
        ? IsDescending ? "מהחדש לישן" : "מהישן לחדש"
        : IsDescending ? "Newest first" : "Oldest first";
    public string EmptyText => _isHebrew ? "אין אירועים בטווח זה" : "No events in this range";
    public string OpenToolTip => _isHebrew
        ? "לחיצה כפולה לפתיחה בלוח השנה"
        : "Double-click to open in Calendar";
    public string ExpandToolTip => _isHebrew
        ? "הרחבה או כיווץ של האירוע"
        : "Expand or collapse event";

    public void RefreshSource(IEnumerable<CalendarEntry> entries, bool isHebrew)
    {
        var expandedIds = CaptureExpandedIds();
        _sourceEntries = entries.ToArray();
        _isHebrew = isHebrew;
        foreach (var property in new[] { nameof(SortLabel), nameof(EmptyText), nameof(OpenToolTip),
            nameof(ExpandToolTip), nameof(SearchLabel), nameof(CloseSearchLabel), nameof(FilterLabel), nameof(PresetChoices) })
            OnPropertyChanged(property);
        RefreshItems();
        RestoreExpandedItems(expandedIds);
    }

    public void SetRange(DateOnly rangeStart, DateOnly rangeEnd)
    {
        if (RangeStart == rangeStart && RangeEnd == rangeEnd)
        {
            return;
        }

        var expandedIds = CaptureExpandedIds();
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
        OnPropertyChanged(nameof(RangeStart));
        OnPropertyChanged(nameof(RangeEnd));
        RefreshItems();
        RestoreExpandedItems(expandedIds);
    }

    public void SetLayoutMode(CalendarLayoutMode layoutMode)
    {
        if (_layoutMode == layoutMode)
        {
            return;
        }

        var expandedIds = CaptureExpandedIds();
        _layoutMode = layoutMode;
        RefreshItems();
        RestoreExpandedItems(expandedIds);
    }

    partial void OnSearchTextChanged(string value)
    {
        if (Mode == CalendarEventsMode.Full) RefreshItems();
    }

    partial void OnSelectedPresetChanged(CalendarEventsPreset value)
    {
        if (Mode == CalendarEventsMode.Full) RefreshItems();
    }

    private (DateOnly From, DateOnly To) GetPresetRange(CalendarEventsPreset preset)
    {
        var today = _todayProvider();
        return preset switch
        {
            CalendarEventsPreset.All => (DateOnly.MinValue, DateOnly.MaxValue),
            CalendarEventsPreset.FromToday => (today, DateOnly.MaxValue),
            CalendarEventsPreset.CurrentWeek => (_weekStartProvider(today), _weekStartProvider(today).AddDays(6)),
            CalendarEventsPreset.CurrentMonth => (new(today.Year, today.Month, 1),
                new(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };
    }

    [RelayCommand]
    private void ToggleSort()
    {
        IsDescending = !IsDescending;
        RefreshItems();
    }

    [RelayCommand]
    private void ActivateEvent(CalendarEventListItemViewModel? item)
    {
        if (item is not null)
        {
            _activateEntry(item.Id, item.Date);
        }
    }

    private void RefreshItems()
    {
        if (Mode == CalendarEventsMode.Full)
        {
            (RangeStart, RangeEnd) = GetPresetRange(SelectedPreset);
            OnPropertyChanged(nameof(RangeStart));
            OnPropertyChanged(nameof(RangeEnd));
        }
        Items = _queryService.Query(
                _sourceEntries, RangeStart, RangeEnd, IsDescending)
            .Where(entry => Mode != CalendarEventsMode.Full
                || entry.Text.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new CalendarEventListItemViewModel(
                entry,
                _previewGenerator.Generate(entry.Text, _layoutMode),
                entry.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)))
            .ToArray();
        OnPropertyChanged(nameof(Items));
    }

    private HashSet<Guid> CaptureExpandedIds() => Items
        .Where(item => item.IsExpanded)
        .Select(item => item.Id)
        .ToHashSet();

    private void RestoreExpandedItems(HashSet<Guid> expandedIds)
    {
        foreach (var item in Items.Where(item => item.HasHiddenContent && expandedIds.Contains(item.Id)))
        {
            item.ToggleExpandedCommand.Execute(null);
        }
    }
}
