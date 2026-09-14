using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public static class CalendarDayPanelSizing
{
    public const double BaselineRegionHeight = 378.2;
    public const double BaselineDefaultHeight = 106;
    public const double BaselineMaximumHeight = 268;
    public const double CompactMinimumHeight = 92;
    public const double LargeMinimumHeight = 180;
    public const double EditorMinimumHeight = 122;
    public const double DefaultHeightRatio = BaselineDefaultHeight / BaselineRegionHeight;
    public const double MaximumHeightRatio = BaselineMaximumHeight / BaselineRegionHeight;

    public static double GetDefaultHeight(
        double regionHeight,
        CalendarLayoutMode layoutMode = CalendarLayoutMode.Compact) => Clamp(
        regionHeight * DefaultHeightRatio,
        GetMaximumHeight(regionHeight),
        GetMinimumHeight(layoutMode, isEditorOpen: false));

    public static double GetMaximumHeight(double regionHeight) => Math.Max(
        CompactMinimumHeight,
        regionHeight * MaximumHeightRatio);

    public static double GetMinimumHeight(CalendarLayoutMode layoutMode, bool isEditorOpen)
    {
        var layoutMinimum = layoutMode == CalendarLayoutMode.Large
            ? LargeMinimumHeight
            : CompactMinimumHeight;
        return isEditorOpen
            ? Math.Max(layoutMinimum, EditorMinimumHeight)
            : layoutMinimum;
    }

    public static double Clamp(
        double height,
        double maximumHeight,
        double minimumHeight = CompactMinimumHeight) =>
        Math.Clamp(height, minimumHeight, Math.Max(minimumHeight, maximumHeight));
}

public sealed class CalendarDayPanelMinimumHeightConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture) => CalendarDayPanelSizing.GetMinimumHeight(
        values.ElementAtOrDefault(0) is CalendarLayoutMode mode
            ? mode
            : CalendarLayoutMode.Compact,
        values.ElementAtOrDefault(1) is true);

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
