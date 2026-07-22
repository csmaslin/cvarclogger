using System;
using System.Globalization;
using System.Windows.Data;

namespace CvarcLogger.App.Converters;

/// <summary>Binds a RadioButton's IsChecked to one value of an enum-valued property: the button is
/// checked when the property equals the ConverterParameter, and checking it sets the property to that
/// value. Unchecking returns Binding.DoNothing (the newly-checked button in the group sets the value).
/// Used for the CAT Source (Off / USB / Internet) selector.</summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is not null)
            return Enum.Parse(targetType, parameter.ToString()!);
        return Binding.DoNothing;
    }
}
