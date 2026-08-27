using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CvarcLogger.App.Converters;

/// <summary>Shows the bound element only when the QSO's Mode has an ADIF Sub-Mode: "PSK" (PSK31),
/// "DIGITALVOICE" (DMR/DSTAR — rigctld can't tell these apart, see RigModeMapper), or "SSB", whose
/// Sub-Mode (USB/LSB) rigctld reports directly (see RigModeMapper.ToCvarcLoggerSubMode).</summary>
public class SubModeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string mode &&
        (string.Equals(mode, "PSK", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(mode, "DIGITALVOICE", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(mode, "SSB", StringComparison.OrdinalIgnoreCase))
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
