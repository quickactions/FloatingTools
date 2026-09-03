using System.Windows;
using CommunityToolkit.Mvvm.Input;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.ViewModels;

public sealed partial class SavedWordItemViewModel(
    SavedWord item,
    Action<SavedWord> copy,
    Func<SavedWord, Task> remove)
{
    public SavedWord Item { get; } = item;

    public string SourceText => Item.SourceText;

    public string PrimaryTranslation => Item.PrimaryTranslation;

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
    private void Copy() => copy(Item);

    [RelayCommand]
    private Task RemoveAsync() => remove(Item);
}
