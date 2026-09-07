using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.Converters;

/// <summary>Direction for Notes title TextBlocks; neutral titles retain LTR/left.</summary>
public sealed class NoteTitleDirectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var direction = TextDirectionResolver.Resolve(value as string);
        if (targetType == typeof(FlowDirection)) return direction.ToFlowDirection();
        return direction.ToPhysicalTextAlignment();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
