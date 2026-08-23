using System.Globalization;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Cabrillo;

/// <summary>Writes a Cabrillo v3 contest log. Header fields come from CabrilloContestInfo (supplied by
/// the operator at export time); QSO lines are formatted per the Cabrillo v3 spec:
///   QSO: freq_khz mode YYYY-MM-DD HHMM sent_call sent_rst sent_exch rcvd_call rcvd_rst rcvd_exch
/// Only frequency (kHz), mode, date, time and callsigns are strictly required per record. Missing
/// RST defaults to 599 (CW/digital) or 59 (phone). Missing exchange is left blank.</summary>
public static class CabrilloWriter
{
    public static void WriteAll(TextWriter writer, CabrilloContestInfo info, IEnumerable<Qso> qsos, string myCallsign)
    {
        writer.WriteLine("START-OF-LOG: 3.0");
        WriteHeaderIfSet(writer, "CALLSIGN", info.Callsign);
        WriteHeaderIfSet(writer, "CONTEST", info.Contest);
        WriteHeaderIfSet(writer, "CATEGORY-OPERATOR", info.CategoryOperator);
        WriteHeaderIfSet(writer, "CATEGORY-ASSISTED", info.CategoryAssisted);
        WriteHeaderIfSet(writer, "CATEGORY-BAND", info.CategoryBand);
        WriteHeaderIfSet(writer, "CATEGORY-MODE", info.CategoryMode);
        WriteHeaderIfSet(writer, "CATEGORY-POWER", info.CategoryPower);
        WriteHeaderIfSet(writer, "CATEGORY-STATION", info.CategoryStation);
        WriteHeaderIfSet(writer, "CATEGORY-TRANSMITTER", info.CategoryTransmitter);
        WriteHeaderIfSet(writer, "CATEGORY-OVERLAY", info.CategoryOverlay);
        WriteHeaderIfSet(writer, "CLAIMED-SCORE", info.ClaimedScore);
        WriteHeaderIfSet(writer, "CLUB", info.Club);
        WriteHeaderIfSet(writer, "LOCATION", info.Location);
        WriteHeaderIfSet(writer, "NAME", info.Name);
        WriteHeaderIfSet(writer, "ADDRESS", info.Address);
        WriteHeaderIfSet(writer, "ADDRESS-CITY", info.AddressCity);
        WriteHeaderIfSet(writer, "ADDRESS-STATE-PROVINCE", info.AddressStateProvince);
        WriteHeaderIfSet(writer, "ADDRESS-POSTALCODE", info.AddressPostalCode);
        WriteHeaderIfSet(writer, "ADDRESS-COUNTRY", info.AddressCountry);
        WriteHeaderIfSet(writer, "OPERATORS", info.Operators);
        WriteHeaderIfSet(writer, "EMAIL", info.Email);
        WriteHeaderIfSet(writer, "CREATED-BY", "CVARC Logger");
        WriteHeaderIfSet(writer, "SOAPBOX", info.SoapBox);

        foreach (var qso in qsos.OrderBy(q => q.QsoDateTimeOnUtc))
        {
            writer.WriteLine(FormatQsoLine(qso, info, myCallsign));
        }

        writer.WriteLine("END-OF-LOG:");
    }

    private static void WriteHeaderIfSet(TextWriter writer, string field, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            writer.WriteLine($"{field}: {value}");
    }

    private static string FormatQsoLine(Qso qso, CabrilloContestInfo info, string myCallsign)
    {
        int freqKhz = qso.FrequencyMhz.HasValue
            ? (int)Math.Round(qso.FrequencyMhz.Value * 1000m)
            : BandToDefaultKhz(qso.Band);

        string mode = MapMode(qso.Mode);
        string date = qso.QsoDateTimeOnUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string time = qso.QsoDateTimeOnUtc.ToString("HHmm", CultureInfo.InvariantCulture);
        string sentCall = string.IsNullOrWhiteSpace(myCallsign) ? info.Callsign : myCallsign;
        string sentRst = string.IsNullOrWhiteSpace(qso.RstSent) ? DefaultRst(mode) : qso.RstSent;
        string sentExch = qso.StxSerial?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        string rcvdCall = qso.Callsign;
        string rcvdRst = string.IsNullOrWhiteSpace(qso.RstRcvd) ? DefaultRst(mode) : qso.RstRcvd;
        string rcvdExch = qso.SrxSerial?.ToString(CultureInfo.InvariantCulture)
            ?? qso.Class ?? qso.ArrlSection ?? qso.State ?? string.Empty;

        return string.Format(CultureInfo.InvariantCulture,
            "QSO: {0,5} {1,2} {2} {3} {4,-13} {5,-3} {6,-6} {7,-13} {8,-3} {9,-6}",
            freqKhz, mode, date, time, sentCall, sentRst, sentExch, rcvdCall, rcvdRst, rcvdExch);
    }

    private static string MapMode(string mode) => mode?.ToUpperInvariant() switch
    {
        "CW" => "CW",
        "SSB" or "USB" or "LSB" or "AM" or "FM" => "PH",
        "RTTY" => "RY",
        null or "" => "PH",
        _ => "DG"
    };

    private static string DefaultRst(string mode) => mode == "PH" ? "59" : "599";

    private static int BandToDefaultKhz(string band) => band?.ToLowerInvariant() switch
    {
        "160m" => 1800,
        "80m" => 3500,
        "60m" => 5330,
        "40m" => 7000,
        "30m" => 10100,
        "20m" => 14000,
        "17m" => 18068,
        "15m" => 21000,
        "12m" => 24890,
        "10m" => 28000,
        "6m" => 50000,
        "2m" => 144000,
        "70cm" => 432000,
        _ => 0
    };
}
