using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FloatingTools.App.SharedUi.Controls;

public sealed class SettingsPageShell : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(SettingsPageShell),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BackCommandProperty = DependencyProperty.Register(
        nameof(BackCommand),
        typeof(ICommand),
        typeof(SettingsPageShell));

    public static readonly DependencyProperty IsBackVisibleProperty = DependencyProperty.Register(
        nameof(IsBackVisible),
        typeof(bool),
        typeof(SettingsPageShell),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsHeaderVisibleProperty = DependencyProperty.Register(
        nameof(IsHeaderVisible),
        typeof(bool),
        typeof(SettingsPageShell),
        new PropertyMetadata(true));

    public static readonly DependencyProperty BackToolTipProperty = DependencyProperty.Register(
        nameof(BackToolTip),
        typeof(string),
        typeof(SettingsPageShell),
        new PropertyMetadata("Back"));

    public static readonly DependencyProperty BackAutomationNameProperty = DependencyProperty.Register(
        nameof(BackAutomationName),
        typeof(string),
        typeof(SettingsPageShell),
        new PropertyMetadata("Back"));

    public static readonly DependencyProperty BodyMarginProperty = DependencyProperty.Register(
        nameof(BodyMargin),
        typeof(Thickness),
        typeof(SettingsPageShell),
        new PropertyMetadata(new Thickness(14, 4, 0, 12)));

    public static readonly DependencyProperty BodyScrollMarginProperty = DependencyProperty.Register(
        nameof(BodyScrollMargin),
        typeof(Thickness),
        typeof(SettingsPageShell),
        new PropertyMetadata(new Thickness(0)));

    public static readonly DependencyProperty TitleFlowDirectionProperty = DependencyProperty.Register(
        nameof(TitleFlowDirection),
        typeof(FlowDirection),
        typeof(SettingsPageShell),
        new PropertyMetadata(FlowDirection.LeftToRight));

    public static readonly DependencyProperty TitleTextAlignmentProperty = DependencyProperty.Register(
        nameof(TitleTextAlignment),
        typeof(TextAlignment),
        typeof(SettingsPageShell),
        new PropertyMetadata(TextAlignment.Left));

    public static readonly DependencyProperty HeaderFlowDirectionProperty = DependencyProperty.Register(
        nameof(HeaderFlowDirection),
        typeof(FlowDirection),
        typeof(SettingsPageShell),
        new PropertyMetadata(FlowDirection.LeftToRight));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public bool IsBackVisible
    {
        get => (bool)GetValue(IsBackVisibleProperty);
        set => SetValue(IsBackVisibleProperty, value);
    }

    public bool IsHeaderVisible
    {
        get => (bool)GetValue(IsHeaderVisibleProperty);
        set => SetValue(IsHeaderVisibleProperty, value);
    }

    public string BackToolTip
    {
        get => (string)GetValue(BackToolTipProperty);
        set => SetValue(BackToolTipProperty, value);
    }

    public string BackAutomationName
    {
        get => (string)GetValue(BackAutomationNameProperty);
        set => SetValue(BackAutomationNameProperty, value);
    }

    public Thickness BodyMargin
    {
        get => (Thickness)GetValue(BodyMarginProperty);
        set => SetValue(BodyMarginProperty, value);
    }

    public Thickness BodyScrollMargin
    {
        get => (Thickness)GetValue(BodyScrollMarginProperty);
        set => SetValue(BodyScrollMarginProperty, value);
    }

    public FlowDirection TitleFlowDirection
    {
        get => (FlowDirection)GetValue(TitleFlowDirectionProperty);
        set => SetValue(TitleFlowDirectionProperty, value);
    }

    public TextAlignment TitleTextAlignment
    {
        get => (TextAlignment)GetValue(TitleTextAlignmentProperty);
        set => SetValue(TitleTextAlignmentProperty, value);
    }

    public FlowDirection HeaderFlowDirection
    {
        get => (FlowDirection)GetValue(HeaderFlowDirectionProperty);
        set => SetValue(HeaderFlowDirectionProperty, value);
    }
}
