using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public sealed partial class FrequentWordItemViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private readonly ISavedWordsService _savedWordsService;
    private readonly Action<string?> _setErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveActionLabel))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaved;

    [ObservableProperty]
    private string _copyActionLabel = "Copy";

    public FrequentWordItemViewModel(
        FrequentWord item,
        IClipboardService clipboardService,
        ISavedWordsService savedWordsService,
        Action<string?> setErrorMessage)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        _clipboardService = clipboardService
            ?? throw new ArgumentNullException(nameof(clipboardService));
        _savedWordsService = savedWordsService
            ?? throw new ArgumentNullException(nameof(savedWordsService));
        _setErrorMessage = setErrorMessage
            ?? throw new ArgumentNullException(nameof(setErrorMessage));
        _isSaved = _savedWordsService.Contains(SourceText, PrimaryTranslation);
    }

    public FrequentWord Item { get; }

    public string SourceText => Item.SourceText;

    public string PrimaryTranslation => Item.PrimaryTranslation;

    public string SaveActionLabel => IsSaved ? "Saved" : "Save";

    private TextDirectionResolution SourceDirectionResolution =>
        TextDirectionResolver.Resolve(SourceText);

    private TextDirectionResolution TranslationDirectionResolution =>
        TextDirectionResolver.Resolve(PrimaryTranslation);

    public FlowDirection SourceFlowDirection =>
        SourceDirectionResolution.ToFlowDirection();

    public TextAlignment SourceTextAlignment =>
        SourceDirectionResolution.ToPhysicalTextAlignment();

    public FlowDirection TranslationFlowDirection =>
        TranslationDirectionResolution.ToFlowDirection();

    public TextAlignment TranslationTextAlignment =>
        TranslationDirectionResolution.ToPhysicalTextAlignment();

    [RelayCommand]
    private void Copy()
    {
        try
        {
            _clipboardService.SetText(PrimaryTranslation);
            CopyActionLabel = "Copied";
            _setErrorMessage(null);
        }
        catch (Exception)
        {
            CopyActionLabel = "Copy";
            _setErrorMessage("Could not copy the frequent translation.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            var result = await _savedWordsService.AddAsync(
                SourceText,
                PrimaryTranslation,
                Item.SourceLanguage,
                Item.TargetLanguage);
            if (result == SavedWordAddResult.LimitReached)
            {
                _setErrorMessage("Saved words limit reached (200).");
                return;
            }

            IsSaved = _savedWordsService.Contains(SourceText, PrimaryTranslation);
            _setErrorMessage(null);
        }
        catch (Exception)
        {
            _setErrorMessage("Could not save this frequent word.");
        }
    }

    private bool CanSave() => !IsSaved;
}
