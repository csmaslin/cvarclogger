using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CvarcLogger.App.Converters;

/// <summary>true -> Visible, false -> Collapsed. Used for the Log Mode preset field visibility on
/// QsoEntryView -- see QsoEntryViewModel's Show* properties and CvarcLogger.Core.UiStandards.QsoEntryModeFields.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
