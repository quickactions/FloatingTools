using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FloatingTools.App.Controls;

namespace FloatingTools.App.Converters;

public sealed class ResponsiveImageDimensionConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (values.Length < 3
            || values[0] is not double requestedWidth
            || values[1] is not double availableWidth
            || values[2] is not double aspectRatio)
        {
            return DependencyProperty.UnsetValue;
        }

        var size = ResponsiveImageSizeCalculator.Calculate(
            requestedWidth,
            availableWidth,
            aspectRatio);
        return string.Equals(parameter as string, "Height", StringComparison.OrdinalIgnoreCase)
            ? size.Height
            : size.Width;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object? parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}
