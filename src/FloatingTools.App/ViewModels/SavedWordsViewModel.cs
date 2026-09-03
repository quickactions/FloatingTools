using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.App.ViewModels;

public partial class SavedWordsViewModel : ObservableObject
{
    private readonly ISavedWordsService _savedWordsService;
    private readonly IClipboardService _clipboardService;
    private readonly ISavedWordsExportService _exportService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public SavedWordsViewModel(
        ISavedWordsService savedWordsService,
        IClipboardService clipboardService,
        ISavedWordsExportService exportService)
    {
        _savedWordsService = savedWordsService
            ?? throw new ArgumentNullException(nameof(savedWordsService));
        _clipboardService = clipboardService
            ?? throw new ArgumentNullException(nameof(clipboardService));
        _exportService = exportService
            ?? throw new ArgumentNullException(nameof(exportService));
        _savedWordsService.Changed += OnSavedWordsChanged;
        RefreshItems();
    }

    public ObservableCollection<SavedWordItemViewModel> Items { get; } = [];

    public string CountDisplay =>
        $"Saved Words {_savedWordsService.Items.Count} / {ISavedWordsService.MaximumItemCount}";

    public bool IsEmpty => _savedWordsService.Items.Count == 0;

    public bool HasNoMatches => !IsEmpty && Items.Count == 0;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnSearchQueryChanged(string value) => RefreshItems();

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportCsvAsync() => ExportAsync(SavedWordsExportFormat.Csv);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportTextAsync() => ExportAsync(SavedWordsExportFormat.Text);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportPdfAsync() => ExportAsync(SavedWordsExportFormat.Pdf);

    private async Task ExportAsync(SavedWordsExportFormat format)
    {
        try
        {
            await _exportService.ExportAsync(_savedWordsService.Items, format);
            ErrorMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "Could not export saved words.";
        }
    }

    private bool CanExport() => _savedWordsService.Items.Count > 0;

    private void Copy(SavedWord item)
    {
        try
        {
            _clipboardService.SetText(item.PrimaryTranslation);
            ErrorMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "Could not copy the saved translation.";
        }
    }

    private async Task RemoveAsync(SavedWord item)
    {
        try
        {
            await _savedWordsService.RemoveAsync(item.Id);
            ErrorMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "Could not remove the saved word.";
        }
    }

    private void OnSavedWordsChanged(object? sender, EventArgs e) => RefreshItems();

    private void RefreshItems()
    {
        var query = SearchQuery.Trim();
        var matchingItems = _savedWordsService.Items
            .Where(item => query.Length == 0
                || item.SourceText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.PrimaryTranslation.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.SavedAt)
            .Select(item => new SavedWordItemViewModel(item, Copy, RemoveAsync))
            .ToArray();

        Items.Clear();
        foreach (var item in matchingItems)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoMatches));
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportTextCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
    }
}
