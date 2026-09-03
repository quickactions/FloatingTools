using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.ViewModels;

public partial class FrequentWordsViewModel : ObservableObject
{
    public const int MaximumVisibleItemCount = 20;

    private readonly IFrequentWordsService _frequentWordsService;
    private readonly ISavedWordsService _savedWordsService;
    private readonly IClipboardService _clipboardService;
    private readonly ISavedWordsExportService _exportService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortToolTip))]
    private bool _isAscending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public FrequentWordsViewModel(
        IFrequentWordsService frequentWordsService,
        ISavedWordsService savedWordsService,
        IClipboardService clipboardService,
        ISavedWordsExportService exportService)
    {
        _frequentWordsService = frequentWordsService
            ?? throw new ArgumentNullException(nameof(frequentWordsService));
        _savedWordsService = savedWordsService
            ?? throw new ArgumentNullException(nameof(savedWordsService));
        _clipboardService = clipboardService
            ?? throw new ArgumentNullException(nameof(clipboardService));
        _exportService = exportService
            ?? throw new ArgumentNullException(nameof(exportService));
        _frequentWordsService.Changed += OnItemsChanged;
        _savedWordsService.Changed += OnItemsChanged;
        RefreshItems();
    }

    public ObservableCollection<FrequentWordItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string SortToolTip => IsAscending
        ? "Show most frequent first"
        : "Show least frequent first";

    [RelayCommand]
    private void ToggleSort() => IsAscending = !IsAscending;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportCsvAsync() => ExportAsync(SavedWordsExportFormat.Csv);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportTextAsync() => ExportAsync(SavedWordsExportFormat.Text);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportPdfAsync() => ExportAsync(SavedWordsExportFormat.Pdf);

    partial void OnIsAscendingChanged(bool value) => RefreshItems();

    private async Task ExportAsync(SavedWordsExportFormat format)
    {
        try
        {
            await _exportService.ExportAsync(
                Items.Select(ToSavedWord).ToArray(),
                format,
                title: "Frequent Words",
                fileNameStem: "frequent-words",
                includeSavedAt: false);
            ErrorMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "Could not export frequent words.";
        }
    }

    private bool CanExport() => Items.Count > 0;

    private void OnItemsChanged(object? sender, EventArgs e) => RefreshItems();

    private void RefreshItems()
    {
        var topItems = _frequentWordsService.Items
            .OrderByDescending(item => item.UsageCount)
            .ThenByDescending(item => item.LastUsedAt)
            .ThenBy(item => item.NormalizedSourceKey, StringComparer.Ordinal)
            .Take(MaximumVisibleItemCount)
            .ToArray();
        var displayed = IsAscending ? topItems.Reverse() : topItems;

        Items.Clear();
        foreach (var item in displayed)
        {
            Items.Add(new FrequentWordItemViewModel(
                item,
                _clipboardService,
                _savedWordsService,
                message => ErrorMessage = message));
        }

        OnPropertyChanged(nameof(IsEmpty));
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportTextCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
    }

    private static SavedWord ToSavedWord(FrequentWordItemViewModel item) => new()
    {
        Id = item.Item.Id,
        SourceText = item.SourceText,
        PrimaryTranslation = item.PrimaryTranslation,
        SourceLanguage = item.Item.SourceLanguage,
        TargetLanguage = item.Item.TargetLanguage,
        SavedAt = item.Item.LastUsedAt
    };
}
