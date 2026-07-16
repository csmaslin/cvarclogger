using System.Globalization;
using System.Windows.Data;

namespace CvarcLogger.App.Converters;

/// <summary>Converts a stored UTC DateTime (Qso.QsoDateTimeOnUtc is always UTC by convention) to the
/// operator's local time for display. Explicitly stamps DateTimeKind.Utc before converting rather than
/// trusting the value's own Kind, since SQLite/EF Core round-trips can lose Kind and leave it Unspecified.</summary>
public class UtcToLocalTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return null;
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
