using System.Globalization;
using System.Text;
using CvarcLogger.Core.Adif;

namespace CvarcLogger.Core.Csv;

/// <summary>Exports QSO records to CSV format using ADIF field names as column headers.
/// All values are properly escaped for CSV format (quoted if they contain comma, newline, or quote).</summary>
public static class CsvWriter
{
    private static readonly string[] ColumnHeaders =
    {
        "CALL", "QSO_DATE", "QSO_DATE_OFF", "TIME_ON", "TIME_OFF", "BAND", "MODE", "SUBMODE",
        "FREQ", "FREQ_RX", "RST_SENT", "RST_RCVD", "NAME", "GRIDSQUARE", "CNTY", "STATE", "COUNTRY",
        "ARRL_SECT", "DXCC", "CONT", "CQZ", "ITUZ", "TX_PWR", "QSL_SENT", "QSL_RCVD", "QSLSDATE", "QSLRDATE",
        "LOTW_QSL_SENT", "LOTW_QSL_RCVD", "LOTW_QSLSDATE", "LOTW_QSLRDATE", "QSL_VIA", "COMMENT",
        "STATION_CALLSIGN", "OPERATOR", "MY_GRIDSQUARE", "MY_STATE", "MY_CNTY",
        "CONTEST_ID", "STX", "SRX", "APP_CVARCLOGGER_CITY", "APP_CVARCLOGGER_QTH", "APP_CVARCLOGGER_OP",
        "APP_QRZLOG_STATUS", "APP_QRZLOG_QSLDATE",
        "MY_SOTA_REF", "SOTA_REF", "MY_SIG_INFO", "SIG_INFO",
        "PRECEDENCE", "CHECK", "CLASS", "APP_CVARCLOGGER_SKCCNR", "APP_CVARCLOGGER_MYSKCCNR"
    };

    public static void WriteAll(TextWriter writer, IEnumerable<AdifRecord> records)
    {
        WriteHeader(writer);
        foreach (var record in records)
        {
            WriteRecord(writer, record);
        }
    }

    private static void WriteHeader(TextWriter writer)
    {
        writer.WriteLine(string.Join(",", ColumnHeaders.Select(EscapeCsvValue)));
    }

    private static void WriteRecord(TextWriter writer, AdifRecord record)
    {
        var values = ColumnHeaders.Select(header => EscapeCsvValue(record.Get(header) ?? string.Empty));
        writer.WriteLine(string.Join(",", values));
    }

    /// <summary>Escapes a value for CSV output. Quotes the value if it contains comma, newline, carriage return,
    /// or double-quote characters. Double quotes within the value are escaped by doubling them.</summary>
    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(',') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
