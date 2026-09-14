using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.ViewModels;

public partial class CalendarToolViewModel : ObservableObject
{
    public const int MaximumEntriesPerDate = 5;

    private readonly AppSettings _appSettings;
    private readonly IAppSettingsStore? _settingsStore;
    private readonly ICalendarLanguageResolver _languageResolver;
    private readonly IHolidayProvider _holidayProvider;
    private readonly ICalendarStore? _calendarStore;
    private readonly IClipboardService _clipboardService;
    private readonly CalendarEntrySearchService _searchService;
    private readonly Func<DateOnly> _todayProvider;
    private readonly CultureInfo _systemCulture;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private CultureInfo _displayCulture;
    private List<CalendarEntry> _entries = [];
    private DateOnly _displayedDate;
    private DateOnly? _selectedDate;
    private CalendarView _currentView;
    private CalendarDisplayLanguage _displayLanguage;

    public CalendarLayoutMode LayoutMode { get; private set; } = CalendarLayoutMode.Compact;

    [ObservableProperty]
    private bool _isHeaderExpanded;

    [ObservableProperty]
    private bool _isDayPanelExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCalendarPage))]
    [NotifyPropertyChangedFor(nameof(IsEventsPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    private CalendarPage _currentPage = CalendarPage.Calendar;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _searchValidationMessage;

    [ObservableProperty]
    private bool _isAddEventHintVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveEvent))]
    private string _eventDraftText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddingEvent))]
    [NotifyPropertyChangedFor(nameof(IsEditingExistingEvent))]
    private Guid? _editingEntryId;

    [ObservableProperty]
    private bool _isEventEditorOpen;

    [ObservableProperty]
    private string? _operationErrorMessage;

    [ObservableProperty]
    private string? _settingsErrorMessage;

    [ObservableProperty]
    private bool _isInitialized;

    public CalendarToolViewModel(
        CalendarSettings settings,
        ICalendarLanguageResolver languageResolver,
        IHolidayProvider holidayProvider,
        Func<DateOnly>? todayProvider = null,
        CultureInfo? systemCulture = null,
        ICalendarStore? calendarStore = null,
        IClipboardService? clipboardService = null,
        CalendarEntrySearchService? searchService = null)
        : this(
            new AppSettings { Calendar = settings },
            settingsStore: null,
            languageResolver,
            holidayProvider,
            calendarStore,
            clipboardService,
            searchService,
            todayProvider,
            systemCulture,
            initialize: true)
    {
    }

    public CalendarToolViewModel(
        AppSettings appSettings,
        IAppSettingsStore settingsStore,
        ICalendarLanguageResolver languageResolver,
        IHolidayProvider holidayProvider,
        ICalendarStore calendarStore,
        IClipboardService clipboardService,
        CalendarEntrySearchService? searchService = null,
        Func<DateOnly>? todayProvider = null,
        CultureInfo? systemCulture = null)
        : this(
            appSettings,
            settingsStore,
            languageResolver,
            holidayProvider,
            calendarStore,
            clipboardService,
            searchService,
            todayProvider,
            systemCulture,
            initialize: true)
    {
    }

    private CalendarToolViewModel(
        AppSettings appSettings,
        IAppSettingsStore? settingsStore,
        ICalendarLanguageResolver languageResolver,
        IHolidayProvider holidayProvider,
        ICalendarStore? calendarStore,
        IClipboardService? clipboardService,
        CalendarEntrySearchService? searchService,
        Func<DateOnly>? todayProvider,
        CultureInfo? systemCulture,
        bool initialize)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _appSettings.Calendar ??= new CalendarSettings();
        _settingsStore = settingsStore;
        _languageResolver = languageResolver
            ?? throw new ArgumentNullException(nameof(languageResolver));
        _holidayProvider = holidayProvider
            ?? throw new ArgumentNullException(nameof(holidayProvider));
        _calendarStore = calendarStore;
        _clipboardService = clipboardService ?? new NullClipboardService();
        _searchService = searchService ?? new CalendarEntrySearchService();
        _todayProvider = todayProvider ?? (() => DateOnly.FromDateTime(DateTime.Today));
        _systemCulture = systemCulture ?? CultureInfo.CurrentCulture;
        _displayLanguage = _languageResolver.Resolve(Settings.Language);
        _displayCulture = CreateGregorianDisplayCulture(_displayLanguage);
        _currentView = Enum.IsDefined(Settings.DefaultView)
            ? Settings.DefaultView
            : CalendarView.Month;
        var today = _todayProvider();
        _displayedDate = CalendarSupportedDateRange.Contains(today)
            ? today
            : CalendarSupportedDateRange.Minimum;
        IsInitialized = calendarStore is null;
        RefreshSettingChoices();
        RefreshPresentation();
    }

    public event Action<Guid>? EntryVisibilityRequested;

    private CalendarSettings Settings => _appSettings.Calendar;

    public CalendarDisplayLanguage DisplayLanguage => _displayLanguage;

    public bool IsCalendarPage => CurrentPage == CalendarPage.Calendar;

    public bool IsEventsPage => CurrentPage == CalendarPage.Events;

    public bool IsSettingsPage => CurrentPage == CalendarPage.Settings;

    public string HeaderTitle => IsHebrew ? "לוח שנה" : "Calendar";

    public string TodayLabel => IsHebrew ? "היום" : "Today";

    public string SearchPlaceholder => IsHebrew ? "חיפוש תאריך או אירוע" : "Search date or event";

    public string SettingsLabel => IsHebrew ? "הגדרות" : "Settings";

    public string CalendarSettingsTitle => IsHebrew ? "הגדרות לוח שנה" : "Calendar settings";

    public string LanguageLabel => IsHebrew ? "שפה" : "Language";

    public string DefaultViewLabel => IsHebrew ? "תצוגת ברירת מחדל" : "Default view";

    public string FirstDayLabel => IsHebrew ? "היום הראשון בשבוע" : "First day of week";

    public string ShowHolidaysLabel => IsHebrew ? "הצג חגים" : "Show holidays";

    public string MonthLabel => IsHebrew ? "חודש" : "Month";

    public string WeekLabel => IsHebrew ? "שבוע" : "Week";

    public string NoEventsText => IsHebrew ? "אין אירועים" : "No events";

    public string EventsHeading => IsHebrew ? "אירועים" : "Events";

    public string AddEventMenuLabel => IsHebrew ? "הוסף אירוע" : "Add event";

    public string EventsActionToolTip => IsHebrew
        ? "הצגת אירועים בתקופה המוצגת"
        : "Show events in the displayed period";

    public string BackToCalendarToolTip => IsHebrew ? "חזרה ללוח השנה" : "Back to Calendar";

    public string SelectDayText => IsHebrew ? "בחרו יום" : "Select a day";

    public string AddEventLabel => IsHebrew ? "הוספת אירוע" : "Add event";

    public string EditEventLabel => IsHebrew ? "עריכת אירוע" : "Edit event";

    public string EventPlaceholder => IsHebrew ? "מה מתוכנן?" : "What is planned?";

    public string SelectDayToAddEventHint => IsHebrew
        ? "בחר יום או תאריך כדי להוסיף אירוע"
        : "Select a day or date to add an event";

    public string SaveLabel => IsHebrew ? "שמירה" : "Save";

    public string CancelLabel => IsHebrew ? "ביטול" : "Cancel";

    public string EditLabel => IsHebrew ? "עריכה" : "Edit";

    public string CopyLabel => IsHebrew ? "העתקה" : "Copy";

    public string DeleteLabel => IsHebrew ? "מחיקה" : "Delete";

    public FlowDirection ContentFlowDirection => IsHebrew
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;

    // Pixel-verified Quick Chat mapping: Left under an RTL flow is physically right.
    public TextAlignment ContentTextAlignment => TextAlignment.Left;

    public HorizontalAlignment ContentHorizontalAlignment => IsHebrew
        ? HorizontalAlignment.Right
        : HorizontalAlignment.Left;

    public HorizontalAlignment TodayBadgeHorizontalAlignment => IsHebrew
        ? HorizontalAlignment.Left
        : HorizontalAlignment.Right;

    public CalendarView CurrentView => _currentView;

    public bool IsMonthView => CurrentView == CalendarView.Month;

    public bool IsWeekView => CurrentView == CalendarView.Week;

    public DateOnly DisplayedDate => _displayedDate;

    public DateOnly? SelectedDate => _selectedDate;

    public DateOnly VisibleRangeStart => CurrentView == CalendarView.Month
        ? new DateOnly(_displayedDate.Year, _displayedDate.Month, 1)
        : GetDisplayWeekStart(_displayedDate);

    public DateOnly VisibleRangeEnd => CurrentView == CalendarView.Month
        ? VisibleRangeStart.AddMonths(1).AddDays(-1)
        : VisibleRangeStart.AddDays(6);

    public DayOfWeek FirstDayOfWeek => ResolveFirstDayOfWeek();

    public bool CanNavigatePrevious => CurrentView == CalendarView.Month
        ? new DateOnly(_displayedDate.Year, _displayedDate.Month, 1) > CalendarSupportedDateRange.Minimum
        : GetDisplayWeekStart(_displayedDate) > CalendarSupportedDateRange.Minimum;

    public bool CanNavigateNext => CurrentView == CalendarView.Month
        ? new DateOnly(_displayedDate.Year, _displayedDate.Month, 1)
            < new DateOnly(CalendarSupportedDateRange.Maximum.Year, CalendarSupportedDateRange.Maximum.Month, 1)
        : GetDisplayWeekStart(_displayedDate)
            < CalendarSupportedDateRange.Maximum.AddDays(-6);

    public bool CanGoToToday => CalendarSupportedDateRange.Contains(_todayProvider());

    public bool IsAddingEvent => IsEventEditorOpen && EditingEntryId is null;

    public bool IsEditingExistingEvent => IsEventEditorOpen && EditingEntryId is not null;

    public bool CanSaveEvent => IsEventEditorOpen && !string.IsNullOrWhiteSpace(EventDraftText);

    public bool CanAddEvent => _selectedDate is not { } date || HasEntryCapacity(date);

    public IReadOnlyList<string> WeekdayHeaders { get; private set; } = [];

    public IReadOnlyList<CalendarDayCellViewModel> MonthDays { get; private set; } = [];

    public int MonthWeekRowCount => MonthDays.Count / 7;

    public IReadOnlyList<WeekDaySectionViewModel> WeekDays { get; private set; } = [];

    public DayPanelViewModel DayPanel { get; private set; } = null!;

    public IReadOnlyList<CalendarSearchResultViewModel> SearchResults { get; private set; } = [];

    public CalendarEventsViewModel? EventsViewModel { get; private set; }

    public CalendarEventsViewModel? ContextualEventsViewModel { get; private set; }

    public void SetLayoutMode(CalendarLayoutMode layoutMode)
    {
        if (LayoutMode == layoutMode)
        {
            return;
        }

        LayoutMode = layoutMode;
        OnPropertyChanged(nameof(LayoutMode));
        EventsViewModel?.SetLayoutMode(layoutMode);
        ContextualEventsViewModel?.SetLayoutMode(layoutMode);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDayContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsCompactPanelExpanded))]
    private bool _isContextualEventsOpen;

    public bool IsDayContentVisible => !IsContextualEventsOpen;
    public bool IsCompactPanelExpanded => IsContextualEventsOpen || IsDayPanelExpanded;

    partial void OnIsDayPanelExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(IsCompactPanelExpanded));

    public IReadOnlyList<CalendarSettingChoice<CalendarLanguageMode>> LanguageChoices { get; private set; } = [];

    public IReadOnlyList<CalendarSettingChoice<CalendarView>> ViewChoices { get; private set; } = [];

    public IReadOnlyList<CalendarSettingChoice<FirstDayOfWeekMode>> FirstDayChoices { get; private set; } = [];

    public void RefreshApplicationLanguage()
    {
        if (Settings.Language == CalendarLanguageMode.UseAppLanguage)
        {
            ApplyLanguage();
        }
    }

    public string PeriodTitle { get; private set; } = string.Empty;

    public string ContextualEventsTitle => $"{EventsHeading} — {PeriodTitle}";

    /// <summary>
    /// Explicit directional fragments used only by the Hebrew Week-title XAML.
    /// </summary>
    public HebrewWeekTitleParts? HebrewWeekTitle { get; private set; }

    public bool HasHebrewWeekTitle => HebrewWeekTitle is not null;

    public bool IsHebrewSameMonthWeekTitle => HebrewWeekTitle is { IsCrossMonth: false };

    public bool IsHebrewCrossMonthWeekTitle => HebrewWeekTitle is { IsCrossMonth: true };

    public string TodayIndicatorText { get; private set; } = string.Empty;

    public string TodayDateText { get; private set; } = string.Empty;

    public CalendarLanguageMode SelectedLanguage
    {
        get => Settings.Language;
        set
        {
            var normalized = Enum.IsDefined(value) ? value : CalendarLanguageMode.UseAppLanguage;
            if (normalized == Settings.Language
                || !TryPersistSetting(() => Settings.Language, previous => Settings.Language = previous,
                    () => Settings.Language = normalized))
            {
                return;
            }

            ApplyLanguage();
            OnPropertyChanged();
        }
    }

    public CalendarView SelectedDefaultView
    {
        get => Settings.DefaultView;
        set => SetView(Enum.IsDefined(value) ? value : CalendarView.Month, persist: true);
    }

    public FirstDayOfWeekMode SelectedFirstDayOfWeek
    {
        get => Settings.FirstDayOfWeek;
        set
        {
            var normalized = Enum.IsDefined(value) ? value : FirstDayOfWeekMode.System;
            if (normalized == Settings.FirstDayOfWeek
                || !TryPersistSetting(() => Settings.FirstDayOfWeek,
                    previous => Settings.FirstDayOfWeek = previous,
                    () => Settings.FirstDayOfWeek = normalized))
            {
                return;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(FirstDayOfWeek));
            RefreshPresentation();
        }
    }

    public bool ShowHolidays
    {
        get => Settings.ShowHolidays;
        set
        {
            if (value == Settings.ShowHolidays
                || !TryPersistSetting(() => Settings.ShowHolidays,
                    previous => Settings.ShowHolidays = previous,
                    () => Settings.ShowHolidays = value))
            {
                return;
            }

            OnPropertyChanged();
            RefreshPresentation();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        try
        {
            var state = await _calendarStore!.LoadAsync(cancellationToken);
            var sourceEntries = state.Entries ?? [];
            var validEntries = sourceEntries
                .Where(entry => entry is not null
                    && CalendarSupportedDateRange.Contains(entry.Date)
                    && !string.IsNullOrWhiteSpace(entry.Text))
                .Select(CloneEntry)
                .ToList();
            if (validEntries.Count != sourceEntries.Count)
            {
                OperationErrorMessage = IsHebrew
                    ? "חלק מאירועי לוח השנה אינם תקינים ולא נטענו."
                    : "Some invalid Calendar entries were not loaded.";
            }

            _entries = validEntries;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            OperationErrorMessage = PersistenceFailureText();
        }
        finally
        {
            IsInitialized = true;
            RefreshPresentation();
        }
    }

    public async Task PrepareForExitAsync(CancellationToken cancellationToken = default)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        _mutationLock.Release();
    }

    [RelayCommand]
    private void ToggleHeader()
    {
        IsHeaderExpanded = !IsHeaderExpanded;
        if (IsHeaderExpanded)
        {
            SearchValidationMessage = null;
            UpdateSearchResults();
        }
    }

    [RelayCommand]
    private void ToggleDayPanel() => IsDayPanelExpanded = !IsDayPanelExpanded;

    [RelayCommand(CanExecute = nameof(CanGoToToday))]
    private void GoToToday()
    {
        var today = _todayProvider();
        if (!CalendarSupportedDateRange.Contains(today))
        {
            OperationErrorMessage = OutOfRangeText();
            return;
        }

        NavigateAndSelect(today);
        IsHeaderExpanded = false;
    }

    [RelayCommand(CanExecute = nameof(CanNavigatePrevious))]
    private void NavigatePrevious()
    {
        _displayedDate = CurrentView == CalendarView.Month
            ? new DateOnly(_displayedDate.Year, _displayedDate.Month, 1).AddMonths(-1)
            : GetDisplayWeekStart(_displayedDate).AddDays(-7);
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateNext))]
    private void NavigateNext()
    {
        _displayedDate = CurrentView == CalendarView.Month
            ? new DateOnly(_displayedDate.Year, _displayedDate.Month, 1).AddMonths(1)
            : GetDisplayWeekStart(_displayedDate).AddDays(7);
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanSelectDate))]
    private void SelectDate(DateOnly date)
    {
        CloseContextualEvents();
        HighlightedEntryId = null;
        IsAddEventHintVisible = false;
        CancelEventEditor();
        NavigateAndSelect(date);
    }

    private static bool CanSelectDate(DateOnly date) => CalendarSupportedDateRange.Contains(date);

    [RelayCommand]
    private void ClearSelection()
    {
        _selectedDate = null;
        HighlightedEntryId = null;
        IsAddEventHintVisible = false;
        CancelEventEditor();
        RefreshPresentation();
    }

    [RelayCommand]
    private void ShowMonth() => SetView(CalendarView.Month, persist: true);

    [RelayCommand]
    private void ShowWeek() => SetView(CalendarView.Week, persist: true);

    [RelayCommand(CanExecute = nameof(CanBeginQuickAdd))]
    private void BeginQuickAdd()
    {
        CloseContextualEvents();
        IsDayPanelExpanded = true;
        CurrentPage = CalendarPage.Calendar;
        IsHeaderExpanded = false;
        OperationErrorMessage = null;
        if (_selectedDate is not null)
        {
            BeginAddEvent();
            return;
        }

        // No day selected: say so and stop. Picking the date is the calendar's
        // job, so there is no second date-entry surface here.
        CancelEventEditor();
        IsAddEventHintVisible = true;
    }

    [RelayCommand(CanExecute = nameof(CanBeginAddEvent))]
    private void BeginAddEvent()
    {
        IsDayPanelExpanded = true;
        if (_selectedDate is null)
        {
            BeginQuickAdd();
            return;
        }

        if (!HasEntryCapacity(_selectedDate.Value))
        {
            OperationErrorMessage = EventLimitText();
            return;
        }

        EditingEntryId = null;
        EventDraftText = string.Empty;
        OperationErrorMessage = null;
        IsEventEditorOpen = true;
        NotifyEditorStateChanged();
    }

    [RelayCommand]
    private void BeginEditEvent(CalendarEntryItemViewModel? entry)
    {
        if (entry is null || !_entries.Any(item => item.Id == entry.Id))
        {
            return;
        }

        EditingEntryId = entry.Id;
        EventDraftText = entry.Text;
        OperationErrorMessage = null;
        IsEventEditorOpen = true;
        NotifyEditorStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSaveEvent))]
    private async Task SaveEventAsync(CancellationToken cancellationToken)
    {
        if (_selectedDate is not { } date || !CalendarSupportedDateRange.Contains(date))
        {
            OperationErrorMessage = OutOfRangeText();
            return;
        }

        var text = EventDraftText.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var next = _entries.Select(CloneEntry).ToList();
        if (EditingEntryId is { } editingId)
        {
            var existing = next.SingleOrDefault(entry => entry.Id == editingId);
            if (existing is null)
            {
                return;
            }

            existing.Text = text;
        }
        else
        {
            if (!HasEntryCapacity(date))
            {
                OperationErrorMessage = EventLimitText();
                return;
            }

            next.Add(new CalendarEntry { Date = date, Text = text });
        }

        if (!await TryPersistEntriesAsync(next, cancellationToken))
        {
            return;
        }

        CancelEventEditor();
    }

    [RelayCommand]
    private void CancelEventEditor()
    {
        IsEventEditorOpen = false;
        EditingEntryId = null;
        EventDraftText = string.Empty;
        NotifyEditorStateChanged();
    }

    [RelayCommand]
    private async Task DeleteEventAsync(
        CalendarEntryItemViewModel? entry,
        CancellationToken cancellationToken)
    {
        if (entry is null || !_entries.Any(item => item.Id == entry.Id))
        {
            return;
        }

        var next = _entries
            .Where(item => item.Id != entry.Id)
            .Select(CloneEntry)
            .ToList();
        if (await TryPersistEntriesAsync(next, cancellationToken)
            && EditingEntryId == entry.Id)
        {
            CancelEventEditor();
        }
    }

    [RelayCommand]
    private void CopyEvent(CalendarEntryItemViewModel? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Text))
        {
            return;
        }

        try
        {
            _clipboardService.SetText(entry.Text);
            OperationErrorMessage = null;
        }
        catch
        {
            OperationErrorMessage = IsHebrew
                ? "לא ניתן היה להעתיק את האירוע."
                : "Could not copy the event.";
        }
    }

    [RelayCommand]
    private void SubmitSearch()
    {
        SearchValidationMessage = null;
        if (CalendarDateParser.TryParse(SearchText, out var date))
        {
            if (!CalendarSupportedDateRange.Contains(date))
            {
                SearchValidationMessage = OutOfRangeText();
                return;
            }

            IsHeaderExpanded = false;
            NavigateAndSelect(date);
            return;
        }

        if (LooksLikeDateInput(SearchText))
        {
            SearchValidationMessage = InvalidDateText();
        }
    }

    [RelayCommand]
    private void SelectSearchResult(CalendarSearchResultViewModel? result)
    {
        if (result is null || !CalendarSupportedDateRange.Contains(result.Date))
        {
            return;
        }

        IsHeaderExpanded = false;
        ActivateEntry(result.EntryId, result.Date);
    }

    [RelayCommand]
    private void OpenContextualEvents()
    {
        IsHeaderExpanded = false;
        ContextualEventsViewModel = new CalendarEventsViewModel(
            CalendarEventsMode.Contextual,
            VisibleRangeStart,
            VisibleRangeEnd,
            _entries,
            IsHebrew,
            ActivateEntry,
            layoutMode: LayoutMode);
        OnPropertyChanged(nameof(ContextualEventsViewModel));
        IsContextualEventsOpen = true;
    }

    [RelayCommand]
    private void CloseContextualEvents() => IsContextualEventsOpen = false;

    [RelayCommand]
    private void OpenEvents()
    {
        IsHeaderExpanded = false;
        if (EventsViewModel is null)
        {
            EventsViewModel = new CalendarEventsViewModel(
                CalendarEventsMode.Full, DateOnly.MinValue, DateOnly.MaxValue,
                _entries, IsHebrew, ActivateEntry, todayProvider: _todayProvider,
                weekStartProvider: GetDisplayWeekStart, layoutMode: LayoutMode);
        }
        else
        {
            EventsViewModel.RefreshSource(_entries, IsHebrew);
        }
        OnPropertyChanged(nameof(EventsViewModel));
        CurrentPage = CalendarPage.Events;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        IsHeaderExpanded = false;
        CurrentPage = CalendarPage.Settings;
        SettingsErrorMessage = null;
    }

    [RelayCommand]
    private void BackToCalendar() => CurrentPage = CalendarPage.Calendar;

    public Guid? HighlightedEntryId { get; private set; }

    partial void OnSearchTextChanged(string value)
    {
        SearchValidationMessage = null;
        UpdateSearchResults();
    }

    partial void OnEventDraftTextChanged(string value) =>
        SaveEventCommand.NotifyCanExecuteChanged();

    private void SetView(CalendarView view, bool persist)
    {
        if (_currentView == view && Settings.DefaultView == view)
        {
            return;
        }

        if (persist && Settings.DefaultView != view
            && !TryPersistSetting(() => Settings.DefaultView,
                previous => Settings.DefaultView = previous,
                () => Settings.DefaultView = view))
        {
            return;
        }

        _currentView = view;
        OnPropertyChanged(nameof(CurrentView));
        OnPropertyChanged(nameof(IsMonthView));
        OnPropertyChanged(nameof(IsWeekView));
        OnPropertyChanged(nameof(SelectedDefaultView));
        RefreshPresentation();
    }

    private void NavigateAndSelect(DateOnly date, bool preserveHighlight = false)
    {
        if (!CalendarSupportedDateRange.Contains(date))
        {
            OperationErrorMessage = OutOfRangeText();
            return;
        }

        _displayedDate = date;
        _selectedDate = date;
        IsDayPanelExpanded = true;
        if (!preserveHighlight)
        {
            HighlightedEntryId = null;
        }

        IsAddEventHintVisible = false;
        RefreshPresentation();
    }

    private async Task<bool> TryPersistEntriesAsync(
        List<CalendarEntry> next,
        CancellationToken cancellationToken)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            if (_calendarStore is not null)
            {
                await _calendarStore.SaveAsync(
                    new CalendarState { Entries = next.Select(CloneEntry).ToList() },
                    cancellationToken);
            }

            _entries = next;
            OperationErrorMessage = null;
            RefreshPresentation();
            UpdateSearchResults();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            OperationErrorMessage = PersistenceFailureText();
            return false;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private bool TryPersistSetting<T>(
        Func<T> getPrevious,
        Action<T> restore,
        Action update)
    {
        var previous = getPrevious();
        update();
        if (_settingsStore is null || _settingsStore.Save(_appSettings))
        {
            SettingsErrorMessage = null;
            return true;
        }

        restore(previous);
        SettingsErrorMessage = IsHebrew
            ? "לא ניתן היה לשמור את ההגדרות."
            : "Could not save settings.";
        return false;
    }

    private void ApplyLanguage()
    {
        _displayLanguage = _languageResolver.Resolve(Settings.Language);
        _displayCulture = CreateGregorianDisplayCulture(_displayLanguage);
        RefreshSettingChoices();
        foreach (var property in new[]
        {
            nameof(DisplayLanguage), nameof(HeaderTitle), nameof(TodayLabel),
            nameof(SearchPlaceholder), nameof(SettingsLabel), nameof(CalendarSettingsTitle),
            nameof(LanguageLabel), nameof(DefaultViewLabel), nameof(FirstDayLabel),
            nameof(ShowHolidaysLabel), nameof(MonthLabel), nameof(WeekLabel),
            nameof(NoEventsText), nameof(EventsHeading), nameof(AddEventMenuLabel),
            nameof(EventsActionToolTip), nameof(BackToCalendarToolTip),
            nameof(SelectDayText), nameof(AddEventLabel),
            nameof(EditEventLabel), nameof(EventPlaceholder), nameof(SelectDayToAddEventHint),
            nameof(SaveLabel), nameof(CancelLabel),
            nameof(EditLabel), nameof(CopyLabel), nameof(DeleteLabel),
            nameof(ContentFlowDirection), nameof(ContentTextAlignment),
            nameof(ContentHorizontalAlignment), nameof(TodayBadgeHorizontalAlignment)
        })
        {
            OnPropertyChanged(property);
        }

        RefreshPresentation();
        UpdateSearchResults();
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedDefaultView));
        OnPropertyChanged(nameof(SelectedFirstDayOfWeek));
        OnPropertyChanged(nameof(ShowHolidays));
    }

    private void RefreshSettingChoices()
    {
        LanguageChoices =
        [
            new(CalendarLanguageMode.UseAppLanguage, IsHebrew ? "שפת היישום" : "Use app language"),
            new(CalendarLanguageMode.English, "English"),
            new(CalendarLanguageMode.Hebrew, "עברית")
        ];
        ViewChoices =
        [
            new(CalendarView.Month, MonthLabel),
            new(CalendarView.Week, WeekLabel)
        ];
        FirstDayChoices =
        [
            new(FirstDayOfWeekMode.System, IsHebrew ? "מערכת" : "System"),
            new(FirstDayOfWeekMode.Sunday, IsHebrew ? "יום ראשון" : "Sunday"),
            new(FirstDayOfWeekMode.Monday, IsHebrew ? "יום שני" : "Monday")
        ];
        OnPropertyChanged(nameof(LanguageChoices));
        OnPropertyChanged(nameof(ViewChoices));
        OnPropertyChanged(nameof(FirstDayChoices));
    }

    private void RefreshPresentation()
    {
        var today = _todayProvider();
        WeekdayHeaders = BuildWeekdayHeaders();
        MonthDays = BuildMonthDays(today);
        WeekDays = BuildWeekDays(today);
        DayPanel = BuildDayPanel();
        PeriodTitle = BuildPeriodTitle();
        HebrewWeekTitle = BuildHebrewWeekTitle();
        if (IsContextualEventsOpen && ContextualEventsViewModel is not null)
        {
            ContextualEventsViewModel.SetRange(VisibleRangeStart, VisibleRangeEnd);
            ContextualEventsViewModel.RefreshSource(_entries, IsHebrew);
        }
        TodayDateText = CalendarSupportedDateRange.Contains(today)
            ? FormatCompactDate(today)
            : string.Empty;
        TodayIndicatorText = TodayDateText.Length == 0
            ? TodayLabel
            : $"{TodayLabel} · {TodayDateText}";

        foreach (var property in new[]
        {
            nameof(DisplayedDate), nameof(SelectedDate), nameof(WeekdayHeaders),
            nameof(MonthDays), nameof(MonthWeekRowCount), nameof(WeekDays),
            nameof(DayPanel), nameof(PeriodTitle), nameof(ContextualEventsTitle),
            nameof(HebrewWeekTitle), nameof(HasHebrewWeekTitle),
            nameof(IsHebrewSameMonthWeekTitle), nameof(IsHebrewCrossMonthWeekTitle),
            nameof(TodayIndicatorText), nameof(TodayDateText), nameof(CanNavigatePrevious),
            nameof(CanNavigateNext), nameof(CanGoToToday), nameof(CanAddEvent),
            nameof(VisibleRangeStart), nameof(VisibleRangeEnd)
        })
        {
            OnPropertyChanged(property);
        }

        NavigatePreviousCommand.NotifyCanExecuteChanged();
        NavigateNextCommand.NotifyCanExecuteChanged();
        GoToTodayCommand.NotifyCanExecuteChanged();
        BeginQuickAddCommand.NotifyCanExecuteChanged();
        BeginAddEventCommand.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<string> BuildWeekdayHeaders() => Enumerable.Range(0, 7)
        .Select(offset => (DayOfWeek)(((int)FirstDayOfWeek + offset) % 7))
        .Select(day => _displayCulture.DateTimeFormat.AbbreviatedDayNames[(int)day])
        .ToArray();

    private IReadOnlyList<CalendarDayCellViewModel> BuildMonthDays(DateOnly today)
    {
        var monthStart = new DateOnly(_displayedDate.Year, _displayedDate.Month, 1);
        var gridStart = StartOfWeekUnbounded(monthStart);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var cellCount = monthEnd <= gridStart.AddDays(34) ? 35 : 42;
        return Enumerable.Range(0, cellCount)
            .Select(offset => gridStart.AddDays(offset))
            .Select(date =>
            {
                var isSupported = CalendarSupportedDateRange.Contains(date);
                return new CalendarDayCellViewModel(
                    date,
                    date.Year == monthStart.Year && date.Month == monthStart.Month,
                    isSupported && date == today,
                    isSupported && date == _selectedDate,
                    isSupported && GetHolidayNames(date).Count > 0,
                    isSupported && _entries.Any(entry => entry.Date == date),
                    isSupported);
            })
            .ToArray();
    }

    private IReadOnlyList<WeekDaySectionViewModel> BuildWeekDays(DateOnly today)
    {
        var weekStart = GetDisplayWeekStart(_displayedDate);
        return Enumerable.Range(0, 7)
            .Select(offset => weekStart.AddDays(offset))
            .Select(date => new WeekDaySectionViewModel(
                date,
                CalendarDateFormatter.FormatWeekHeading(
                    date, _displayLanguage, _displayCulture),
                date == today,
                date == _selectedDate,
                GetHolidayNames(date),
                GetEntryItems(date)))
            .ToArray();
    }

    private DayPanelViewModel BuildDayPanel()
    {
        if (_selectedDate is not { } date)
        {
            return new DayPanelViewModel(null, string.Empty, [], [], SelectDayText, NoEventsText);
        }

        return new DayPanelViewModel(
            date,
            CalendarDateFormatter.FormatDayPanelDate(
                date, _displayLanguage, _displayCulture),
            GetHolidayNames(date),
            GetEntryItems(date),
            SelectDayText,
            NoEventsText);
    }

    private IReadOnlyList<CalendarEntryItemViewModel> GetEntryItems(DateOnly date) => _entries
        .Where(entry => entry.Date == date)
        .Select(entry => new CalendarEntryItemViewModel(
            entry.Id,
            entry.Date,
            entry.Text,
            entry.Id == HighlightedEntryId))
        .ToArray();

    private string BuildPeriodTitle()
    {
        if (CurrentView == CalendarView.Month)
        {
            return CalendarDateFormatter.FormatMonthYear(_displayedDate, _displayCulture);
        }

        var start = GetDisplayWeekStart(_displayedDate);
        var end = start.AddDays(6);
        return CalendarDateFormatter.FormatWeekRange(
            start, end, _displayLanguage, _displayCulture);
    }

    private HebrewWeekTitleParts? BuildHebrewWeekTitle()
    {
        if (CurrentView != CalendarView.Week || !IsHebrew)
        {
            return null;
        }

        var start = GetDisplayWeekStart(_displayedDate);
        return CalendarDateFormatter.FormatHebrewWeekTitleParts(
            start, start.AddDays(6), _displayCulture);
    }

    private void UpdateSearchResults()
    {
        SearchResults = _searchService.Search(_entries, SearchText, _todayProvider())
            .Where(entry => CalendarSupportedDateRange.Contains(entry.Date))
            .Select(entry => new CalendarSearchResultViewModel(
                entry.Id,
                entry.Date,
                CalendarDateFormatter.FormatSearchResultDate(
                    entry.Date, _displayLanguage, _displayCulture),
                entry.Text))
            .ToArray();
        OnPropertyChanged(nameof(SearchResults));
    }

    private static bool LooksLikeDateInput(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Any(char.IsDigit)
        && text.All(character => char.IsDigit(character)
            || char.IsWhiteSpace(character)
            || character is '/' or '.' or '-');

    private IReadOnlyList<string> GetHolidayNames(DateOnly date)
    {
        if (!ShowHolidays || !CalendarSupportedDateRange.Contains(date))
        {
            return [];
        }

        return _holidayProvider.GetHolidays(date)
            .Select(holiday => IsHebrew ? holiday.HebrewName : holiday.EnglishName)
            .ToArray();
    }

    private DateOnly StartOfWeekUnbounded(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        return date.AddDays(-offset);
    }

    private DateOnly GetDisplayWeekStart(DateOnly date)
    {
        var start = StartOfWeekUnbounded(date);
        if (start < CalendarSupportedDateRange.Minimum)
        {
            return CalendarSupportedDateRange.Minimum;
        }

        var latestStart = CalendarSupportedDateRange.Maximum.AddDays(-6);
        return start > latestStart ? latestStart : start;
    }

    private DayOfWeek ResolveFirstDayOfWeek() => Settings.FirstDayOfWeek switch
    {
        FirstDayOfWeekMode.Sunday => DayOfWeek.Sunday,
        FirstDayOfWeekMode.Monday => DayOfWeek.Monday,
        _ => _systemCulture.DateTimeFormat.FirstDayOfWeek
    };

    private string FormatCompactDate(DateOnly date) =>
        CalendarDateFormatter.FormatCompactDate(
            date, _displayLanguage, _displayCulture);

    private string InvalidDateText() => CalendarDateValidationMessages.InvalidDate(IsHebrew);

    private string OutOfRangeText() => CalendarDateValidationMessages.UnsupportedDate(IsHebrew);

    private string PersistenceFailureText() => IsHebrew
        ? "לא ניתן היה לשמור את השינויים בלוח השנה."
        : "Could not save Calendar changes.";

    private string EventLimitText() => IsHebrew
        ? "ניתן להוסיף עד חמישה אירועים ביום."
        : "A date can contain up to five events.";

    private bool HasEntryCapacity(DateOnly date) =>
        _entries.Count(entry => entry.Date == date) < MaximumEntriesPerDate;

    private bool CanBeginQuickAdd() =>
        _selectedDate is not { } date || HasEntryCapacity(date);

    private bool CanBeginAddEvent() =>
        _selectedDate is { } date && HasEntryCapacity(date);

    private bool IsHebrew => _displayLanguage == CalendarDisplayLanguage.Hebrew;

    private void ActivateEntry(Guid entryId, DateOnly date)
    {
        if (!CalendarSupportedDateRange.Contains(date))
        {
            return;
        }

        CurrentPage = CalendarPage.Calendar;
        CloseContextualEvents();
        HighlightedEntryId = entryId;
        NavigateAndSelect(date, preserveHighlight: true);
        EntryVisibilityRequested?.Invoke(entryId);
    }

    private void NotifyEditorStateChanged()
    {
        OnPropertyChanged(nameof(IsAddingEvent));
        OnPropertyChanged(nameof(IsEditingExistingEvent));
        OnPropertyChanged(nameof(CanSaveEvent));
        SaveEventCommand.NotifyCanExecuteChanged();
    }

    private static CalendarEntry CloneEntry(CalendarEntry entry) => new()
    {
        Id = entry.Id,
        Date = entry.Date,
        Text = entry.Text ?? string.Empty
    };

    private static CultureInfo CreateGregorianDisplayCulture(CalendarDisplayLanguage language)
    {
        var culture = (CultureInfo)new CultureInfo(
            language == CalendarDisplayLanguage.Hebrew ? "he-IL" : "en-US").Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return culture;
    }
}
