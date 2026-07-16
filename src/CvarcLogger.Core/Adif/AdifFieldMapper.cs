using System.Globalization;
using System.Text.Json;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Adif;

/// <summary>Converts between <see cref="Qso"/> and <see cref="AdifRecord"/>. Any ADIF field with no first-class
/// Qso column round-trips through <see cref="Qso.AdifExtraFieldsJson"/> instead of being dropped.</summary>
public static class AdifFieldMapper
{
    // ADIF has no standard "city" field; APP_CVARCLOGGER_CITY follows the spec's APP_<programid>_<field>
    // convention for application-specific extensions, so it still round-trips instead of being dropped.
    private const string CityFieldName = "APP_CVARCLOGGER_CITY";

    private static readonly string[] MappedFieldNames =
    {
        "CALL", "QSO_DATE", "QSO_DATE_OFF", "TIME_ON", "TIME_OFF", "BAND", "MODE", "SUBMODE",
        "FREQ", "FREQ_RX", "RST_SENT", "RST_RCVD", "NAME", "GRIDSQUARE", "CNTY", "STATE", "COUNTRY",
        "ARRL_SECT", "DXCC", "CONT", "CQZ", "ITUZ", "TX_PWR", "QSL_SENT", "QSL_RCVD", "QSLSDATE", "QSLRDATE",
        "LOTW_QSL_SENT", "LOTW_QSL_RCVD", "LOTW_QSLSDATE", "LOTW_QSLRDATE", "QSL_VIA", "COMMENT",
        "NOTES", "STATION_CALLSIGN", "OPERATOR", "MY_GRIDSQUARE", "MY_STATE", "MY_CNTY",
        "CONTEST_ID", "STX", "SRX", CityFieldName
    };

    private static readonly HashSet<string> MappedFieldNameSet = new(MappedFieldNames, StringComparer.OrdinalIgnoreCase);

    public static Qso ToQso(AdifRecord record)
    {
        var qso = new Qso
        {
            Callsign = record.Get("CALL") ?? string.Empty,
            Band = record.Get("BAND") ?? string.Empty,
            Mode = record.Get("MODE") ?? string.Empty,
            SubMode = record.Get("SUBMODE"),
            RstSent = record.Get("RST_SENT"),
            RstRcvd = record.Get("RST_RCVD"),
            Name = record.Get("NAME"),
            GridSquare = record.Get("GRIDSQUARE"),
            City = record.Get(CityFieldName),
            County = record.Get("CNTY"),
            State = record.Get("STATE"),
            Country = record.Get("COUNTRY"),
            ArrlSection = record.Get("ARRL_SECT"),
            Continent = record.Get("CONT"),
            QslViaCallsign = record.Get("QSL_VIA"),
            Comment = record.Get("COMMENT"),
            Notes = record.Get("NOTES"),
            StationCallsign = record.Get("STATION_CALLSIGN") ?? string.Empty,
            OperatorCallsign = record.Get("OPERATOR"),
            MyGridSquare = record.Get("MY_GRIDSQUARE"),
            MyState = record.Get("MY_STATE"),
            MyCounty = record.Get("MY_CNTY"),
            ContestId = record.Get("CONTEST_ID"),
        };

        var qsoDateTime = ParseAdifDateTime(record.Get("QSO_DATE"), record.Get("TIME_ON"));
        if (qsoDateTime.HasValue) qso.QsoDateTimeOnUtc = qsoDateTime.Value;
        qso.QsoDateTimeOffUtc = ParseAdifDateTime(record.Get("QSO_DATE_OFF") ?? record.Get("QSO_DATE"), record.Get("TIME_OFF"));

        qso.FrequencyMhz = ParseDecimal(record.Get("FREQ"));
        qso.FrequencyRxMhz = ParseDecimal(record.Get("FREQ_RX"));
        qso.DxccEntityCode = ParseInt(record.Get("DXCC"));
        qso.CqZone = ParseInt(record.Get("CQZ"));
        qso.ItuZone = ParseInt(record.Get("ITUZ"));
        qso.TxPowerWatts = ParseDecimal(record.Get("TX_PWR"));
        qso.StxSerial = ParseInt(record.Get("STX"));
        qso.SrxSerial = ParseInt(record.Get("SRX"));

        qso.QslSent = QslStatusFromAdif(record.Get("QSL_SENT"));
        qso.QslRcvd = QslStatusFromAdif(record.Get("QSL_RCVD"));
        qso.QslSentDate = ParseAdifDateTime(record.Get("QSLSDATE"), null);
        qso.QslRcvdDate = ParseAdifDateTime(record.Get("QSLRDATE"), null);

        qso.LotwQslSent = QslStatusFromAdif(record.Get("LOTW_QSL_SENT"));
        qso.LotwQslRcvd = QslStatusFromAdif(record.Get("LOTW_QSL_RCVD"));
        qso.LotwQslSentDate = ParseAdifDateTime(record.Get("LOTW_QSLSDATE"), null);
        qso.LotwQslRcvdDate = ParseAdifDateTime(record.Get("LOTW_QSLRDATE"), null);

        var extra = record.Fields
            .Where(f => !MappedFieldNameSet.Contains(f.Key))
            .ToDictionary(f => f.Key.ToUpperInvariant(), f => f.Value);
        qso.AdifExtraFieldsJson = extra.Count > 0 ? JsonSerializer.Serialize(extra) : null;

        return qso;
    }

    public static AdifRecord ToAdifRecord(Qso qso)
    {
        var record = new AdifRecord();
        record.Set("CALL", qso.Callsign);
        record.Set("QSO_DATE", qso.QsoDateTimeOnUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        record.Set("TIME_ON", qso.QsoDateTimeOnUtc.ToString("HHmmss", CultureInfo.InvariantCulture));
        if (qso.QsoDateTimeOffUtc is { } off)
        {
            record.Set("QSO_DATE_OFF", off.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            record.Set("TIME_OFF", off.ToString("HHmmss", CultureInfo.InvariantCulture));
        }
        record.Set("BAND", qso.Band);
        record.Set("MODE", qso.Mode);
        record.Set("SUBMODE", qso.SubMode);
        record.Set("FREQ", FormatDecimal(qso.FrequencyMhz));
        record.Set("FREQ_RX", FormatDecimal(qso.FrequencyRxMhz));
        record.Set("RST_SENT", qso.RstSent);
        record.Set("RST_RCVD", qso.RstRcvd);
        record.Set("NAME", qso.Name);
        record.Set("GRIDSQUARE", qso.GridSquare);
        record.Set(CityFieldName, qso.City);
        record.Set("CNTY", qso.County);
        record.Set("STATE", qso.State);
        record.Set("COUNTRY", qso.Country);
        record.Set("ARRL_SECT", qso.ArrlSection);
        record.Set("DXCC", qso.DxccEntityCode?.ToString(CultureInfo.InvariantCulture));
        record.Set("CONT", qso.Continent);
        record.Set("CQZ", qso.CqZone?.ToString(CultureInfo.InvariantCulture));
        record.Set("ITUZ", qso.ItuZone?.ToString(CultureInfo.InvariantCulture));
        record.Set("TX_PWR", FormatDecimal(qso.TxPowerWatts));

        record.Set("QSL_SENT", QslStatusToAdif(qso.QslSent));
        record.Set("QSL_RCVD", QslStatusToAdif(qso.QslRcvd));
        record.Set("QSLSDATE", qso.QslSentDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        record.Set("QSLRDATE", qso.QslRcvdDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        record.Set("LOTW_QSL_SENT", QslStatusToAdif(qso.LotwQslSent));
        record.Set("LOTW_QSL_RCVD", QslStatusToAdif(qso.LotwQslRcvd));
        record.Set("LOTW_QSLSDATE", qso.LotwQslSentDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        record.Set("LOTW_QSLRDATE", qso.LotwQslRcvdDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        record.Set("QSL_VIA", qso.QslViaCallsign);
        record.Set("COMMENT", qso.Comment);
        record.Set("NOTES", qso.Notes);
        record.Set("STATION_CALLSIGN", qso.StationCallsign);
        record.Set("OPERATOR", qso.OperatorCallsign);
        record.Set("MY_GRIDSQUARE", qso.MyGridSquare);
        record.Set("MY_STATE", qso.MyState);
        record.Set("MY_CNTY", qso.MyCounty);
        record.Set("CONTEST_ID", qso.ContestId);
        record.Set("STX", qso.StxSerial?.ToString(CultureInfo.InvariantCulture));
        record.Set("SRX", qso.SrxSerial?.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(qso.AdifExtraFieldsJson))
        {
            var extra = JsonSerializer.Deserialize<Dictionary<string, string>>(qso.AdifExtraFieldsJson);
            if (extra != null)
            {
                foreach (var kvp in extra)
                {
                    if (!MappedFieldNameSet.Contains(kvp.Key))
                    {
                        record.Set(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        return record;
    }

    private static DateTime? ParseAdifDateTime(string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        if (!DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return null;

        var t = TimeSpan.Zero;
        if (!string.IsNullOrWhiteSpace(time))
        {
            string normalized = time.Length <= 4 ? time.PadRight(4, '0') : time.PadRight(6, '0');
            string format = normalized.Length <= 4 ? "HHmm" : "HHmmss";
            if (DateTime.TryParseExact(normalized, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tm))
                t = tm.TimeOfDay;
        }

        return DateTime.SpecifyKind(d.Date + t, DateTimeKind.Utc);
    }

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    private static string? FormatDecimal(decimal? value) =>
        value?.ToString("0.######", CultureInfo.InvariantCulture);

    private static string QslStatusToAdif(QslStatus status) => status switch
    {
        QslStatus.Sent => "Y",
        QslStatus.Requested => "R",
        QslStatus.Queued => "Q",
        QslStatus.Verified => "V",
        QslStatus.Ignore => "I",
        _ => "N",
    };

    private static QslStatus QslStatusFromAdif(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "Y" => QslStatus.Sent,
        "R" => QslStatus.Requested,
        "Q" => QslStatus.Queued,
        "V" => QslStatus.Verified,
        "I" => QslStatus.Ignore,
        _ => QslStatus.NotSent,
    };
}
