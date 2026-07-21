using System.Globalization;

namespace CvarcCellLog.Converters;

/// <summary>Maps a "password is hidden" bool onto the show/hide toggle button's label -- Hidden=true
/// (the default, matching Entry.IsPassword's default) shows "Show", so tapping it means "show it".</summary>
public class PasswordVisibilityTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Show" : "Hide";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
