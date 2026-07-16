using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CvarcLogger.App.Converters;

/// <summary>Shows the bound element only when the QSO's Mode is "DATA" — that's the generic bucket
/// rigctld reports for every digital mode (see RigModeMapper), so the Sub-Mode picker (FT8/FT4/RTTY/
/// PSK31/DMR/D-STAR) only makes sense to show once Mode is that generic value.</summary>
public class DataModeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, "DATA", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
