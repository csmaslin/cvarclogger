using System.Globalization;
using CvarcCellLog.Models;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.Converters;

/// <summary>Extracts one column's display text from a Qso -- used by QsoLogPage's dynamically-built
/// row DataTemplate, where ConverterParameter is the LogColumnKey that particular cell shows.</summary>
public class QsoCellValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Qso qso && parameter is LogColumnKey key ? LogColumns.GetValue(qso, key) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
