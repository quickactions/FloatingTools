using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Popups;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Views;

public partial class CalendarToolView : UserControl
{
    private readonly PopupAnchorService _popupAnchorService;
    private CalendarToolViewModel? _viewModel;
    private bool _dayPanelHandleDragged;
    private bool _hasUserSizedDayPanel;
    private double _userDayPanelHeight;
    private bool _isFullEventsSearchOpen;

    public CalendarToolView()
    {
        InitializeComponent();
        _popupAnchorService = new PopupAnchorService(() => CalendarPopupHost);
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += CalendarToolView_OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe(DataContext as CalendarToolViewModel);
        UpdateCalendarLayout();
        UpdateDayPanelSizing();
    }

    public void FocusSearch()
    {
        if (DataContext is CalendarToolViewModel { IsEventsPage: true })
        {
            _isFullEventsSearchOpen = true;
            UpdateFullEventsToolbar();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                FullEventsSearchTextBox.Focus();
                FullEventsSearchTextBox.SelectAll();
            });
            return;
        }
        if (DataContext is CalendarToolViewModel { IsHeaderExpanded: false } viewModel)
        {
            viewModel.ToggleHeaderCommand.Execute(null);
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                CalendarSearchTextBox.Focus();
                CalendarSearchTextBox.SelectAll();
            });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _popupAnchorService.Close();
        Unsubscribe();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Unsubscribe();
        if (IsLoaded)
        {
            Subscribe(e.NewValue as CalendarToolViewModel);
        }
    }

    private void Subscribe(CalendarToolViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.EntryVisibilityRequested += OnEntryVisibilityRequested;
        _viewModel.SetLayoutMode(CalendarLayoutModeResolver.Resolve(ActualWidth));
    }

    private void Unsubscribe()
    {
        if (_viewModel is not null)
        {
            _viewModel.EntryVisibilityRequested -= OnEntryVisibilityRequested;
            _viewModel = null;
        }
    }

    private void ViewSelectorButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_popupAnchorService.IsOpen)
        {
            _popupAnchorService.Close();
            return;
        }

        ShowPopup(
            ViewSelectorButton,
            "CalendarViewSelectorPopupTemplate",
            DataContext);
    }

    private void EventActionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CalendarEntryItemViewModel entry }
            || DataContext is not CalendarToolViewModel owner)
        {
            return;
        }

        ShowPopup(
            (Button)sender,
            "CalendarEventActionsPopupTemplate",
            new EventActionsContext(owner, entry));
        e.Handled = true;
    }

    private void ShowPopup(FrameworkElement target, string templateKey, object? dataContext)
    {
        var template = (DataTemplate)Resources[templateKey];
        var content = (FrameworkElement)template.LoadContent();
        content.DataContext = dataContext;
        _popupAnchorService.Show(new PopupAnchorRequest(target, content, CalendarRoot)
        {
            PreferredPlacement = PopupAnchorPreferredPlacement.Below,
            Gap = 4,
            EdgeMargin = 5,
            CloseOnExternalClick = true,
            SingleInstance = true
        });
    }

    private void PopupOption_OnClick(object sender, RoutedEventArgs e) =>
        _popupAnchorService.Close();

    private void CalendarPeriodRegion_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || HasInteractiveAncestor(source, CalendarPeriodRegion))
        {
            return;
        }

        if (_viewModel?.ClearSelectionCommand.CanExecute(null) == true)
        {
            _viewModel.ClearSelectionCommand.Execute(null);
        }
    }

    private void ContextualEventRow_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2
            || sender is not FrameworkElement { DataContext: CalendarEventListItemViewModel item }
            || e.OriginalSource is DependencyObject source
                && HasButtonAncestor(source, (DependencyObject)sender))
        {
            return;
        }

        var events = (_viewModel ?? DataContext as CalendarToolViewModel) is { } owner
            ? owner.IsEventsPage ? owner.EventsViewModel : owner.ContextualEventsViewModel
            : null;
        if (events?.ActivateEventCommand.CanExecute(item) == true)
        {
            events.ActivateEventCommand.Execute(item);
            e.Handled = true;
        }
    }

    private static bool HasButtonAncestor(DependencyObject source, DependencyObject boundary)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is ButtonBase)
            {
                return true;
            }

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        return false;
    }

    private static bool HasInteractiveAncestor(
        DependencyObject source,
        DependencyObject boundary)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is ButtonBase or TextBoxBase or ScrollBar or Thumb)
            {
                return true;
            }

            if (current is FrameworkElement
                {
                    DataContext: CalendarDayCellViewModel
                        or WeekDaySectionViewModel
                        or CalendarEntryItemViewModel
                        or string
                })
            {
                return true;
            }

            if (ReferenceEquals(current, boundary))
            {
                break;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject value) =>
        value is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(value)
            : LogicalTreeHelper.GetParent(value);

    private void CalendarToolView_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateCalendarLayout();

    private void CalendarPeriodRegion_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateDayPanelSizing();

    private void FullEventsToolbar_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateFullEventsToolbar();

    private void UpdateCalendarLayout()
    {
        var layoutMode = CalendarLayoutModeResolver.Resolve(ActualWidth);
        (_viewModel ?? DataContext as CalendarToolViewModel)?.SetLayoutMode(layoutMode);
        UpdateDayPanelSizing();
        UpdateFullEventsToolbar();
    }

    private void UpdateDayPanelSizing()
    {
        var regionHeight = CalendarPeriodRegion.ActualHeight;
        if (regionHeight <= 0)
        {
            return;
        }

        var maximumHeight = CalendarDayPanelSizing.GetMaximumHeight(regionHeight);
        var layoutMode = CalendarLayoutModeResolver.Resolve(ActualWidth);
        var minimumHeight = CalendarDayPanelSizing.GetMinimumHeight(
            layoutMode,
            (_viewModel ?? DataContext as CalendarToolViewModel)?.IsEventEditorOpen == true);
        DayPanelExpandedContent.MaxHeight = Math.Max(
            maximumHeight,
            minimumHeight);
        if (_hasUserSizedDayPanel)
        {
            _userDayPanelHeight = CalendarDayPanelSizing.Clamp(
                _userDayPanelHeight,
                maximumHeight,
                minimumHeight);
            DayPanelExpandedContent.Height = _userDayPanelHeight;
            return;
        }

        DayPanelExpandedContent.Height = CalendarDayPanelSizing.GetDefaultHeight(
            regionHeight,
            layoutMode);
    }

    private void UpdateFullEventsToolbar()
    {
        if (FullEventsSearchHost is null || FullEventsFilterControls is null) return;
        var wide = CalendarLayoutModeResolver.Resolve(ActualWidth) == CalendarLayoutMode.Large;
        var hasQuery = (DataContext as CalendarToolViewModel)?.EventsViewModel?.SearchText.Length > 0;
        var searchVisible = wide || _isFullEventsSearchOpen || hasQuery;
        FullEventsSearchButton.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
        FullEventsSearchHost.Visibility = searchVisible ? Visibility.Visible : Visibility.Collapsed;
        FullEventsFilterControls.Visibility = wide || !searchVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FullEventsSearchButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (FullEventsSearchHost.IsVisible) CloseFullEventsSearch();
        else FocusSearch();
    }

    private void FullEventsCloseSearchButton_OnClick(object sender, RoutedEventArgs e) => CloseFullEventsSearch();

    private void CloseFullEventsSearch()
    {
        if (DataContext is CalendarToolViewModel { EventsViewModel: { } events }) events.SearchText = string.Empty;
        _isFullEventsSearchOpen = false;
        UpdateFullEventsToolbar();
        if (FullEventsSearchButton.IsVisible) FullEventsSearchButton.Focus();
        else FullEventsSearchTextBox.Focus();
    }

    private void CalendarSearchTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (_viewModel?.SubmitSearchCommand.CanExecute(null) == true)
        {
            _viewModel.SubmitSearchCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void EventEditorTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (sender is TextBox textBox && TryCancelEmptyAddEditor(e.Key, textBox.Text))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _viewModel.SaveEventCommand.CanExecute(null))
        {
            _viewModel.SaveEventCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape
            && _viewModel.CancelEventEditorCommand.CanExecute(null))
        {
            _viewModel.CancelEventEditorCommand.Execute(null);
            e.Handled = true;
        }
    }

    internal bool TryCancelEmptyAddEditor(Key key, string? text)
    {
        var viewModel = _viewModel ?? DataContext as CalendarToolViewModel;
        if (key is not (Key.Back or Key.Delete)
            || !string.IsNullOrEmpty(text)
            || viewModel?.IsAddingEvent != true
            || !viewModel.CancelEventEditorCommand.CanExecute(null))
        {
            return false;
        }

        viewModel.CancelEventEditorCommand.Execute(null);
        return true;
    }

    private void DayPanelSplitter_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if ((_viewModel ?? DataContext as CalendarToolViewModel)?.IsCompactPanelExpanded != true)
        {
            return;
        }

        _dayPanelHandleDragged = true;
        var layoutMode = CalendarLayoutModeResolver.Resolve(ActualWidth);
        var minimumHeight = CalendarDayPanelSizing.GetMinimumHeight(
            layoutMode,
            (_viewModel ?? DataContext as CalendarToolViewModel)?.IsEventEditorOpen == true);
        var requestedHeight = CalendarDayPanelSizing.Clamp(
            DayPanelExpandedContent.ActualHeight - e.VerticalChange,
            CalendarDayPanelSizing.GetMaximumHeight(CalendarPeriodRegion.ActualHeight),
            minimumHeight);
        _hasUserSizedDayPanel = true;
        _userDayPanelHeight = requestedHeight;
        DayPanelExpandedContent.Height = requestedHeight;
    }

    private void DayPanelSplitter_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e) => _dayPanelHandleDragged = false;

    private void DayPanelSplitter_OnPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_dayPanelHandleDragged)
        {
            ToggleDayPanel();
        }
    }

    private void DayPanelSplitter_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }

        ToggleDayPanel();
        e.Handled = true;
    }

    private void ToggleDayPanel()
    {
        var viewModel = _viewModel ?? DataContext as CalendarToolViewModel;
        if (viewModel?.IsContextualEventsOpen == true)
        {
            viewModel.CloseContextualEventsCommand.Execute(null);
            return;
        }
        if (viewModel?.ToggleDayPanelCommand.CanExecute(null) == true)
        {
            viewModel.ToggleDayPanelCommand.Execute(null);
        }
    }

    private void EventEditorTextBox_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is TextBox textBox)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                () =>
                {
                    textBox.Focus();
                    textBox.CaretIndex = textBox.Text.Length;
                    if ((_viewModel ?? DataContext as CalendarToolViewModel)?.IsAddingEvent == true)
                    {
                        ScrollEventEditorIntoView(EventEditorSection);
                    }
                });
        }
    }

    internal void ScrollEventEditorIntoView(FrameworkElement editorBlock)
    {
        DayPanelScrollViewer.UpdateLayout();
        editorBlock.BringIntoView(new Rect(editorBlock.RenderSize));
        DayPanelScrollViewer.UpdateLayout();

        var blockTop = editorBlock.TranslatePoint(new Point(), DayPanelScrollViewer).Y;
        var preferredTop = Math.Max(
            0,
            (DayPanelScrollViewer.ViewportHeight - editorBlock.ActualHeight) * 0.65);
        var targetOffset = Math.Clamp(
            DayPanelScrollViewer.VerticalOffset + blockTop - preferredTop,
            0,
            DayPanelScrollViewer.ScrollableHeight);
        DayPanelScrollViewer.ScrollToVerticalOffset(targetOffset);
    }

    private void OnEntryVisibilityRequested(Guid entryId) => Dispatcher.BeginInvoke(
        DispatcherPriority.Loaded,
        () =>
        {
            var element = FindDescendants<FrameworkElement>(DayPanelEventItems)
                .FirstOrDefault(candidate => candidate.DataContext is CalendarEntryItemViewModel entry
                    && entry.Id == entryId);
            element?.BringIntoView();
        });

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private sealed record EventActionsContext(
        CalendarToolViewModel Owner,
        CalendarEntryItemViewModel Entry);
}
