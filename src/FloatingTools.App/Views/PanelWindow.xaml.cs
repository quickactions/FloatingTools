using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FloatingTools.App.Models;
using FloatingTools.App.Services;
using FloatingTools.App.ViewModels;

namespace FloatingTools.App.Views;

public partial class PanelWindow : Window
{
    private CornerRadius _activeContentCornerRadius;

    public event EventHandler? CloseRequested;

    public PanelWindow(
        FloatingToolbarViewModel viewModel,
        TranslationToolViewModel translationToolViewModel,
        NotesToolViewModel notesToolViewModel,
        QuickChatViewModel quickChatViewModel,
        CalendarToolViewModel calendarToolViewModel,
        SettingsViewModel applicationSettingsViewModel)
    {
        InitializeComponent();
        DataContext = viewModel
            ?? throw new ArgumentNullException(nameof(viewModel));
        TranslationTool.DataContext = translationToolViewModel
            ?? throw new ArgumentNullException(nameof(translationToolViewModel));
        NotesTool.DataContext = notesToolViewModel
            ?? throw new ArgumentNullException(nameof(notesToolViewModel));
        QuickChatTool.DataContext = quickChatViewModel
            ?? throw new ArgumentNullException(nameof(quickChatViewModel));
        CalendarTool.DataContext = calendarToolViewModel
            ?? throw new ArgumentNullException(nameof(calendarToolViewModel));
        ApplicationSettings.DataContext = applicationSettingsViewModel
            ?? throw new ArgumentNullException(nameof(applicationSettingsViewModel));
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FindCommand_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = DataContext is FloatingToolbarViewModel viewModel
            && viewModel.PanelState == PanelState.ActiveTool
            && ToolSupportsSearch(viewModel.ActiveTool);
    }

    private void FindCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (DataContext is not FloatingToolbarViewModel viewModel)
        {
            return;
        }

        switch (viewModel.ActiveTool)
        {
            case ToolId.Translation:
                TranslationTool.FocusSearch();
                break;
            case ToolId.Notes:
                NotesTool.FocusSearch();
                break;
            case ToolId.Calendar:
                CalendarTool.FocusSearch();
                break;
        }
    }

    internal static bool ToolSupportsSearch(ToolId tool) =>
        tool is ToolId.Translation or ToolId.Notes or ToolId.Calendar;

    public void ApplyLayout(
        PanelState panelState,
        PanelSizePreset activeToolPanelSize,
        double availableWidthDip,
        double availableHeightDip,
        DockSide dockSide)
    {
        if (panelState == PanelState.ToolMenu)
        {
            Width = PanelSizeCalculator.ToolMenuWidth;
            Height = PanelSizeCalculator.GetToolMenuHeight(
                Enum.GetValues<ToolId>().Length);
        }
        else
        {
            var size = PanelSizeCalculator.GetActiveToolSize(
                activeToolPanelSize,
                availableWidthDip,
                availableHeightDip);
            Width = size.Width;
            Height = size.Height;
        }

        ApplyCornerRadii(dockSide);
    }

    private void PanelSizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void PanelSizeContextMenu_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu
            || DataContext is not FloatingToolbarViewModel viewModel)
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.IsChecked = item.Tag is PanelSizePreset preset
                && preset == viewModel.ActiveToolPanelSize;
        }
    }

    private void PanelSizeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PanelSizePreset preset }
            && DataContext is FloatingToolbarViewModel viewModel)
        {
            viewModel.SelectPanelSizeCommand.Execute(preset);
        }
    }

    private void ApplyCornerRadii(DockSide dockSide)
    {
        var radii = PanelChromeCornerRadiusCalculator.Calculate(dockSide);

        ToolMenuPanel.CornerRadius = radii.Panel;
        ActiveToolPanel.CornerRadius = radii.Panel;
        ToolMenuHeader.CornerRadius = radii.Header;
        ActiveToolHeader.CornerRadius = radii.Header;
        _activeContentCornerRadius = radii.ActiveContent;
        UpdateActiveToolContentClip();
    }

    private void ActiveToolContent_OnSizeChanged(
        object sender,
        SizeChangedEventArgs e) =>
        UpdateActiveToolContentClip();

    private void UpdateActiveToolContentClip()
    {
        var width = ActiveToolContent.ActualWidth;
        var height = ActiveToolContent.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            ActiveToolContent.Clip = null;
            return;
        }

        var maximumRadius = Math.Min(width, height);
        var bottomRight = Math.Min(_activeContentCornerRadius.BottomRight, maximumRadius);
        var bottomLeft = Math.Min(_activeContentCornerRadius.BottomLeft, maximumRadius);
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            context.LineTo(new Point(width, 0), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(width, height - bottomRight), isStroked: true, isSmoothJoin: false);

            if (bottomRight > 0)
            {
                context.ArcTo(
                    new Point(width - bottomRight, height),
                    new Size(bottomRight, bottomRight),
                    rotationAngle: 0,
                    isLargeArc: false,
                    SweepDirection.Clockwise,
                    isStroked: true,
                    isSmoothJoin: false);
            }

            context.LineTo(new Point(bottomLeft, height), isStroked: true, isSmoothJoin: false);

            if (bottomLeft > 0)
            {
                context.ArcTo(
                    new Point(0, height - bottomLeft),
                    new Size(bottomLeft, bottomLeft),
                    rotationAngle: 0,
                    isLargeArc: false,
                    SweepDirection.Clockwise,
                    isStroked: true,
                    isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        ActiveToolContent.Clip = geometry;
    }
}
