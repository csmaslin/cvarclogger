using System.Globalization;
using System.Windows.Data;

namespace CvarcLogger.App.Converters;

/// <summary>Combines a SotaActivation's Points and Activated flag into its grid display string: the
/// plain point value once activated, or the same value in parentheses -- "(#)" -- beforehand, to show
/// what it would be worth once the 4-contact activation rule is actually met.</summary>
public class PointsDisplayConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [int points, bool activated]) return string.Empty;
        return activated ? points.ToString(culture) : $"({points})";
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
