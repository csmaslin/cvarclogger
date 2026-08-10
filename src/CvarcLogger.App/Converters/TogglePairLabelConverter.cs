using System.Globalization;
using System.Windows.Data;

namespace CvarcLogger.App.Converters;

/// <summary>Joins a sidebar toggle tab's two underlying mode labels into one display string, e.g.
/// "Normal / Contest". Bound to both halves' PickerModeTabs[i].Label via MultiBinding so a rename (Rename
/// Tab, see ColumnPickerWindow) updates the sidebar button text live -- a plain static "X/Y" string on
/// the button, tried first, didn't pick up renames at all. ConverterParameter is the emoji prefix.</summary>
public class TogglePairLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var first = values.Length > 0 ? values[0] as string : null;
        var second = values.Length > 1 ? values[1] as string : null;
        var prefix = parameter as string;
        return $"{prefix}{first} / {second}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
