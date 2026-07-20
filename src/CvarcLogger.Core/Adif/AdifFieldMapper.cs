using System.Globalization;
using System.Text.Json;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Adif;

/// <summary>Converts between <see cref="Qso"/> and <see cref="AdifRecord"/>. Any ADIF field with no first-class
/// Qso column round-trips through <see cref="Qso.AdifExtraFieldsJson"/> instead of being dropped.</summary>
public static class AdifFieldMapper
{
    // ADIF has no standard "city"/"QTH"/secondary-operator-name field; these APP_CVARCLOGGER_* names
    // follow the spec's APP_<programid>_<field> convention for application-specific extensions, so
    // they still round-trip instead of being dropped.
    private const string CityFieldName = "APP_CVARCLOGGER_CITY";
    private const string QthFieldName = "APP_CVARCLOGGER_QTH";
    private const string OpFieldName = "APP_CVARCLOGGER_OP";

    private static readonly string[] MappedFieldNames =
    {
        "CALL", "QSO_DATE", "QSO_DATE_OFF", "TIME_ON", "TIME_OFF", "BAND", "MODE", "SUBMODE",
        "FREQ", "FREQ_RX", "RST_SENT", "RST_RCVD", "NAME", "GRIDSQUARE", "CNTY", "STATE", "COUNTRY",
        "ARRL_SECT", "DXCC", "CONT", "CQZ", "ITUZ", "TX_PWR", "QSL_SENT", "QSL_RCVD", "QSLSDATE", "QSLRDATE",
        "LOTW_QSL_SENT", "LOTW_QSL_RCVD", "LOTW_QSLSDATE", "LOTW_QSLRDATE", "QSL_VIA", "COMMENT",
        "STATION_CALLSIGN", "OPERATOR", "MY_GRIDSQUARE", "MY_STATE", "MY_CNTY",
        "CONTEST_ID", "STX", "SRX", CityFieldName, QthFieldName, OpFieldName,
        "APP_QRZLOG_STATUS", "APP_QRZLOG_QSLDATE",
        "MY_SOTA_REF", "SOTA_REF", "MY_SIG_INFO", "SIG_INFO"
    };

    private static readonly HashSet<string> MappedFieldNameSet = new(MappedFieldNames, StringComparer.OrdinalIgnoreCase);

    public static Qso ToQso(AdifRecord record)
    {
        var (mode, subMode) = NormalizeLegacyDataMode(record.Get("MODE") ?? string.Empty, record.Get("SUBMODE"));
        var qso = new Qso
        {
            Callsign = record.Get("CALL") ?? string.Empty,
            Band = record.Get("BAND") ?? string.Empty,
            Mode = mode,
            SubMode = subMode,
            RstSent = record.Get("RST_SENT"),
            RstRcvd = record.Get("RST_RCVD"),
            Name = record.Get("NAME"),
            GridSquare = record.Get("GRIDSQUARE"),
            City = record.Get(CityFieldName),
            County = ParseCounty(record.Get("CNTY")),
            State = record.Get("STATE"),
            Country = record.Get("COUNTRY"),
            ArrlSection = record.Get("ARRL_SECT"),
            Continent = record.Get("CONT"),
            QslViaCallsign = record.Get("QSL_VIA"),
            Comment = record.Get("COMMENT"),
            StationCallsign = record.Get("STATION_CALLSIGN") ?? string.Empty,
            OperatorCallsign = record.Get("OPERATOR"),
            MyGridSquare = record.Get("MY_GRIDSQUARE"),
            MyState = record.Get("MY_STATE"),
            MyCounty = ParseCounty(record.Get("MY_CNTY")),
            Qth = record.Get(QthFieldName),
            Op = record.Get(OpFieldName),
            ContestId = record.Get("CONTEST_ID"),
            MySotaRef = record.Get("MY_SOTA_REF"),
            SotaRef = record.Get("SOTA_REF"),
            MySigInfo = record.Get("MY_SIG_INFO"),
            SigInfo = record.Get("SIG_INFO"),
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

        // QRZ Logbook downloads track confirmation through its own APP_QRZLOG_STATUS/
        // APP_QRZLOG_QSLDATE fields instead of meaningfully populating the standard QSL_RCVD/QSLRDATE
        // tags (QRZ exports leave those "N"/blank even for confirmed contacts) -- prefer QRZ's own
        // status when present, since it's the actually-informative field for those files.
        string? qrzStatus = record.Get("APP_QRZLOG_STATUS");
        if (!string.IsNullOrWhiteSpace(qrzStatus))
        {
            qso.QslRcvd = QslStatusFromQrzLogStatus(qrzStatus);
            qso.QslRcvdDate = ParseAdifDateTime(record.Get("APP_QRZLOG_QSLDATE"), null) ?? qso.QslRcvdDate;
        }

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
        record.Set("BAND", NormalizeBandForAdif(qso.Band));
        // No Mode/SubMode translation needed here (unlike ToQso) -- qso.Mode/SubMode already match
        // ADIF's real vocabulary directly (see QsoFieldOptions), since "DATA" was removed as a
        // selectable/storable value going forward.
        record.Set("MODE", qso.Mode);
        record.Set("SUBMODE", qso.SubMode);
        record.Set("FREQ", FormatDecimal(qso.FrequencyMhz));
        record.Set("FREQ_RX", FormatDecimal(qso.FrequencyRxMhz));
        record.Set("RST_SENT", qso.RstSent);
        record.Set("RST_RCVD", qso.RstRcvd);
        record.Set("NAME", qso.Name);
        record.Set("GRIDSQUARE", qso.GridSquare);
        record.Set(CityFieldName, qso.City);
        record.Set("CNTY", FormatCounty(qso.State, qso.County));
        record.Set("STATE", qso.State);
        record.Set("COUNTRY", qso.Country);
        record.Set("ARRL_SECT", qso.ArrlSection);
        record.Set("DXCC", qso.DxccEntityCode?.ToString(CultureInfo.InvariantCulture));
        record.Set("CONT", qso.Continent);
        record.Set("CQZ", qso.CqZone?.ToString(CultureInfo.InvariantCulture));
        record.Set("ITUZ", qso.ItuZone?.ToString(CultureInfo.InvariantCulture));
        record.Set("TX_PWR", FormatDecimal(qso.TxPowerWatts));

        record.Set("QSL_SENT", QslSentStatusToAdif(qso.QslSent));
        record.Set("QSL_RCVD", QslRcvdStatusToAdif(qso.QslRcvd));
        record.Set("QSLSDATE", qso.QslSentDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        record.Set("QSLRDATE", qso.QslRcvdDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        record.Set("LOTW_QSL_SENT", QslSentStatusToAdif(qso.LotwQslSent));
        record.Set("LOTW_QSL_RCVD", QslRcvdStatusToAdif(qso.LotwQslRcvd));
        record.Set("LOTW_QSLSDATE", qso.LotwQslSentDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        record.Set("LOTW_QSLRDATE", qso.LotwQslRcvdDate?.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        record.Set("QSL_VIA", qso.QslViaCallsign);
        record.Set("COMMENT", qso.Comment);
        record.Set("STATION_CALLSIGN", qso.StationCallsign);
        record.Set("OPERATOR", qso.OperatorCallsign);
        record.Set("MY_GRIDSQUARE", qso.MyGridSquare);
        record.Set("MY_STATE", qso.MyState);
        record.Set("MY_CNTY", FormatCounty(qso.MyState, qso.MyCounty));
        record.Set(QthFieldName, qso.Qth);
        record.Set(OpFieldName, qso.Op);
        record.Set("CONTEST_ID", qso.ContestId);
        record.Set("STX", qso.StxSerial?.ToString(CultureInfo.InvariantCulture));
        record.Set("SRX", qso.SrxSerial?.ToString(CultureInfo.InvariantCulture));
        record.Set("MY_SOTA_REF", qso.MySotaRef);
        record.Set("SOTA_REF", qso.SotaRef);
        record.Set("MY_SIG_INFO", qso.MySigInfo);
        record.Set("SIG_INFO", qso.SigInfo);

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

    /// <summary>ADIF's Band enumeration token for 222-225 MHz is "1.25M" -- CvarcLogger versions through
    /// 1.27 used the wrong token ("1.2M", see QsoFieldOptions.Bands and CHANGELOG 1.17) internally.
    /// Normalizes at export time so already-logged QSOs still produce spec-correct ADIF without needing
    /// a database migration; QsoFieldOptions.Bands itself was also fixed so newly-logged QSOs store the
    /// correct value directly.</summary>
    private static string NormalizeBandForAdif(string band) =>
        string.Equals(band, "1.2M", StringComparison.OrdinalIgnoreCase) ? "1.25M" : band;

    /// <summary>Backward compatibility for re-importing an .adi file exported by a CvarcLogger version
    /// through 1.27, before Mode/SubMode were changed to match ADIF's real vocabulary directly (see
    /// QsoFieldOptions) -- those old exports contain the literal (never actually valid) ADIF Mode
    /// "DATA", with the real digital mode stashed in SubMode instead. Translates that back into the
    /// current, ADIF-correct Mode/SubMode pair. Everything else -- including files from any other,
    /// non-CvarcLogger software, which never used "DATA" as a mode -- passes through unchanged, since
    /// our Mode/SubMode already match ADIF's own vocabulary and need no further translation.</summary>
    private static (string Mode, string? SubMode) NormalizeLegacyDataMode(string mode, string? subMode)
    {
        if (!string.Equals(mode, "DATA", StringComparison.OrdinalIgnoreCase)) return (mode, subMode);

        return subMode?.Trim().ToUpperInvariant() switch
        {
            "DMR" => ("DIGITALVOICE", "DMR"),
            "D-STAR" => ("DIGITALVOICE", "DSTAR"),
            "FT8" => ("FT8", null),
            "FT4" => ("FT4", null),
            "RTTY" => ("RTTY", null),
            "PSK31" => ("PSK", "PSK31"),
            _ => (mode, subMode),
        };
    }

    /// <summary>ADIF's CNTY/MY_CNTY fields are conventionally written as "ST,County" (e.g. "OH,Franklin")
    /// -- county names repeat across states, so county-hunting software (GridTracker2, DXKeeper, N3FJP,
    /// etc.) relies on the state prefix to know which county was actually worked. Writing the bare county
    /// name without it means that software silently fails to recognize the field at all.</summary>
    private static string? FormatCounty(string? state, string? county)
    {
        if (string.IsNullOrWhiteSpace(county)) return null;
        return string.IsNullOrWhiteSpace(state) ? county : $"{state},{county}";
    }

    /// <summary>Strips the "ST," prefix back off an incoming CNTY/MY_CNTY value -- State already has its
    /// own first-class field, so County only ever stores the bare county name internally.</summary>
    private static string? ParseCounty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        int comma = value.IndexOf(',');
        return comma >= 0 ? value[(comma + 1)..].Trim() : value.Trim();
    }

    /// <summary>QSL_SENT/LOTW_QSL_SENT/EQSL_QSL_SENT enumeration is Y, N, R, Q, I -- no "V" (Verified
    /// only makes sense for a received confirmation, never an outgoing one). QslStatus.Verified degrades
    /// to "Y" here rather than emitting a token this field's enumeration doesn't define -- see
    /// QslStatus.cs, which already documents Queued as "SENT only" and Verified as "RCVD only".</summary>
    private static string QslSentStatusToAdif(QslStatus status) => status switch
    {
        QslStatus.Sent => "Y",
        QslStatus.Requested => "R",
        QslStatus.Queued => "Q",
        QslStatus.Verified => "Y",
        QslStatus.Ignore => "I",
        _ => "N",
    };

    /// <summary>QSL_RCVD/LOTW_QSL_RCVD/EQSL_QSL_RCVD enumeration is Y, N, R, V, I -- no "Q" (Queued only
    /// makes sense for an outgoing card/upload, never an incoming confirmation). QslStatus.Queued
    /// degrades to "R" here (closest received-side equivalent -- something pending) rather than emitting
    /// a token this field's enumeration doesn't define.</summary>
    private static string QslRcvdStatusToAdif(QslStatus status) => status switch
    {
        QslStatus.Sent => "Y",
        QslStatus.Requested => "R",
        QslStatus.Queued => "R",
        QslStatus.Verified => "V",
        QslStatus.Ignore => "I",
        _ => "N",
    };

    /// <summary>Parsing is intentionally direction-agnostic (accepts any of Y/N/R/Q/V/I regardless of
    /// which field it came from) -- an externally-sourced file that puts a technically-wrong-for-that-
    /// field letter in QSL_SENT/QSL_RCVD should still be read faithfully rather than rejected; only
    /// *writing* needs to stay within each field's actual enumeration (see QslSentStatusToAdif/
    /// QslRcvdStatusToAdif above).</summary>
    private static QslStatus QslStatusFromAdif(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "Y" => QslStatus.Sent,
        "R" => QslStatus.Requested,
        "Q" => QslStatus.Queued,
        "V" => QslStatus.Verified,
        "I" => QslStatus.Ignore,
        _ => QslStatus.NotSent,
    };

    /// <summary>Maps QRZ Logbook's own confirmation-status letter (APP_QRZLOG_STATUS) to our QslStatus.
    /// This is QRZ's proprietary vocabulary, distinct from ADIF's standard QSL_RCVD codes above: C =
    /// confirmed, A = reserved/not used yet, N = not confirmed, 2 = confirmation requested, S =
    /// confirmation requested and seen by the other station, R = confirmation requested and
    /// rejected.</summary>
    private static QslStatus QslStatusFromQrzLogStatus(string code) => code.Trim().ToUpperInvariant() switch
    {
        "C" => QslStatus.Sent, // "Yes (received, confirmed)" in our RCVD vocabulary -- see QslStatus.cs
        "2" => QslStatus.Requested,
        "S" => QslStatus.Requested, // seen but not yet acted on -- still just "requested" to us
        "R" => QslStatus.Ignore,    // rejected -- don't count on it
        _ => QslStatus.NotSent,     // "N", and "A" (reserved/unused)
    };
}
