using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public partial class TranslationEntryViewModel : ObservableObject
{
    public const int MaximumAlternativeCount = 3;

    private readonly IClipboardService _clipboardService;
    private readonly ITranslationService? _translationService;
    private readonly ISavedWordsService? _savedWordsService;
    private readonly Action<string?> _setErrorMessage;
    private readonly Action<TranslationEntryViewModel>? _toggleActions;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string _copyFeedback = "Copy";

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Increments only when this entry reveals new content that wasn't
    /// visible a moment ago (expanding, or a newly-loaded alternative) —
    /// never on collapse. The view binds
    /// <see cref="SharedUi.Controls.BringIntoViewOnRevealBehavior"/> to this
    /// so it can ask WPF to scroll the entry into view after layout
    /// catches up, without the view model knowing anything about scrolling.
    /// </summary>
    [ObservableProperty]
    private int _revealRequestVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlternativeActionLabel))]
    [NotifyCanExecuteChangedFor(nameof(RequestAlternativeCommand))]
    private bool _isAlternativeLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlternativeMessage))]
    private string? _alternativeMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlternativeActionToolTip))]
    [NotifyCanExecuteChangedFor(nameof(RequestAlternativeCommand))]
    private bool _isAlternativeExhausted;

    public TranslationEntryViewModel(
        TranslationEntry entry,
        IClipboardService clipboardService,
        ITranslationHistoryStore historyStore,
        Action<string?>? setErrorMessage = null,
        Action<TranslationEntryViewModel>? toggleActions = null,
        ITranslationService? translationService = null,
        ISavedWordsService? savedWordsService = null)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _clipboardService = clipboardService
            ?? throw new ArgumentNullException(nameof(clipboardService));
        ArgumentNullException.ThrowIfNull(historyStore);
        _setErrorMessage = setErrorMessage ?? (_ => { });
        _toggleActions = toggleActions;
        _translationService = translationService;
        _savedWordsService = savedWordsService;
        _isFavorite = savedWordsService?.Contains(SourceText, MainTranslation)
            ?? entry.IsFavorite;
        Entry.IsFavorite = _isFavorite;
        if (_savedWordsService is not null)
        {
            _savedWordsService.Changed += OnSavedWordsChanged;
        }
    }

    public TranslationEntry Entry { get; }

    public ObservableCollection<AlternativeTranslationViewModel> Alternatives { get; } = [];

    public string SourceText => Entry.SourceText;

    public string MainTranslation => Entry.Result.MainTranslation;

    public string? CorrectedSourceText => Entry.Result.CorrectedSourceText;

    public bool HasCorrection =>
        Entry.Result.CorrectionStatus == TranslationCorrectionStatus.Confident
        &&
        CorrectedSourceText is { } correction
        && !correction.Equals(SourceText, StringComparison.Ordinal);

    public bool HasTranslation =>
        Entry.Result.CorrectionStatus != TranslationCorrectionStatus.Ambiguous
        && !string.IsNullOrWhiteSpace(MainTranslation);

    public bool IsAmbiguous =>
        Entry.Result.CorrectionStatus == TranslationCorrectionStatus.Ambiguous;

    public string AmbiguousMessage => IsAmbiguous
        ? "Could not identify the intended word."
        : string.Empty;

    public string CorrectedSourceDisplayText => HasCorrection
        ? $"Corrected: {CorrectedSourceText}"
        : string.Empty;

    private TextDirectionResolution SourceDirectionResolution =>
        TextDirectionResolver.Resolve(SourceText);

    private TextDirectionResolution ResultDirectionResolution =>
        TextDirectionResolver.Resolve(MainTranslation);

    public FlowDirection SourceFlowDirection =>
        SourceDirectionResolution.ToFlowDirection();

    public TextAlignment SourceTextAlignment =>
        SourceDirectionResolution.ToPhysicalTextAlignment();

    public FlowDirection ResultFlowDirection =>
        ResultDirectionResolution.ToFlowDirection();

    public TextAlignment ResultTextAlignment =>
        ResultDirectionResolution.ToPhysicalTextAlignment();

    public bool HasAlternativeMessage =>
        !string.IsNullOrWhiteSpace(AlternativeMessage);

    public string AlternativeActionLabel => IsAlternativeLoading
        ? "Loading…"
        : "Alternative";

    public string AlternativeActionToolTip => IsAlternativeExhausted
        ? "No more alternatives"
        : "Request one more alternative translation";

    public string FavoriteActionLabel => IsFavorite
        ? "Saved"
        : "Save";

    [RelayCommand]
    private void ToggleActions()
    {
        if (_toggleActions is not null)
        {
            _toggleActions(this);
            return;
        }

        IsExpanded = !IsExpanded;
    }

    internal void SetExpanded(bool isExpanded)
    {
        var wasExpanded = IsExpanded;
        IsExpanded = isExpanded;
        if (isExpanded && !wasExpanded)
        {
            RevealRequestVersion++;
        }
    }

    [RelayCommand(CanExecute = nameof(HasTranslation))]
    private void Copy()
    {
        try
        {
            _clipboardService.SetText(MainTranslation);
            CopyFeedback = "Copied";
            _setErrorMessage(null);
        }
        catch (Exception)
        {
            CopyFeedback = "Copy";
            _setErrorMessage("Could not copy the translation.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task ToggleFavoriteAsync()
    {
        if (!CanSave() || _savedWordsService is null)
        {
            return;
        }

        try
        {
            var direction = TranslationDirectionResolver.Resolve(SourceText);
            var result = await _savedWordsService.AddAsync(
                SourceText,
                MainTranslation,
                Entry.Result.DetectedLanguage ?? direction.SourceLanguage,
                Entry.Result.TargetLanguage ?? direction.TargetLanguage);
            if (result == SavedWordAddResult.LimitReached)
            {
                _setErrorMessage("Saved words limit reached (200).");
                return;
            }

            SynchronizeSavedState();
            _setErrorMessage(null);
        }
        catch (Exception)
        {
            _setErrorMessage("Could not save this translation.");
        }
    }

    private bool CanSave() =>
        _savedWordsService is not null
        && HasTranslation
        && !IsFavorite;

    [RelayCommand(
        CanExecute = nameof(CanRequestAlternative),
        AllowConcurrentExecutions = false)]
    private async Task RequestAlternativeAsync(CancellationToken cancellationToken)
    {
        if (!CanRequestAlternative() || _translationService is null)
        {
            return;
        }

        IsAlternativeLoading = true;
        AlternativeMessage = null;

        try
        {
            var direction = TranslationDirectionResolver.Resolve(SourceText);
            var alternative = await _translationService.TranslateAlternativeAsync(
                SourceText,
                Entry.Result.DetectedLanguage ?? direction.SourceLanguage,
                Entry.Result.TargetLanguage ?? direction.TargetLanguage,
                MainTranslation,
                Alternatives.Select(item => item.Text).ToArray(),
                cancellationToken);

            if (alternative is null)
            {
                AlternativeMessage = "No more alternatives.";
                IsAlternativeExhausted = true;
            }
            else
            {
                Alternatives.Add(new AlternativeTranslationViewModel(alternative));
                AlternativeMessage = null;
                if (Alternatives.Count >= MaximumAlternativeCount)
                {
                    IsAlternativeExhausted = true;
                }

                RequestAlternativeCommand.NotifyCanExecuteChanged();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Command cancellation leaves the entry unchanged.
        }
        catch (Exception)
        {
            AlternativeMessage = "Could not load an alternative. Try again.";
        }
        finally
        {
            IsAlternativeLoading = false;
            RevealRequestVersion++;
        }
    }

    private bool CanRequestAlternative() =>
        _translationService is not null
        && HasTranslation
        && !IsAlternativeLoading
        && !IsAlternativeExhausted
        && Alternatives.Count < MaximumAlternativeCount;

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteActionLabel));
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
    }

    private void OnSavedWordsChanged(object? sender, EventArgs e) =>
        SynchronizeSavedState();

    private void SynchronizeSavedState()
    {
        IsFavorite = _savedWordsService?.Contains(SourceText, MainTranslation) == true;
        Entry.IsFavorite = IsFavorite;
    }
}
