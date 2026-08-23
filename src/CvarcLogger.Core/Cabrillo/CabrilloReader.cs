using System.Globalization;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Cabrillo;

/// <summary>Reads a Cabrillo v3 contest log. Returns both the header info and the parsed QSOs.
/// Handles the strict Cabrillo v3 QSO line format:
///   QSO: freq_khz mode YYYY-MM-DD HHMM sent_call sent_rst sent_exch rcvd_call rcvd_rst rcvd_exch
/// Whitespace between fields varies -- some loggers pad, some don't -- so tokens are split on any
/// whitespace rather than fixed positions.</summary>
public static class CabrilloReader
{
    public class ParseResult
    {
        public CabrilloContestInfo Info { get; set; } = new();
        public List<Qso> Qsos { get; set; } = new();
    }

    public static ParseResult ReadAll(string filePath)
    {
        var result = new ParseResult();

        foreach (var line in File.ReadAllLines(filePath))
        {
            string trimmed = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("QSO:", StringComparison.OrdinalIgnoreCase))
            {
                var qso = ParseQsoLine(trimmed);
                if (qso is not null) result.Qsos.Add(qso);
            }
            else if (trimmed.StartsWith("END-OF-LOG:", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            else
            {
                int colon = trimmed.IndexOf(':');
                if (colon > 0)
                {
                    string field = trimmed.Substring(0, colon).Trim().ToUpperInvariant();
                    string value = trimmed.Substring(colon + 1).Trim();
                    ApplyHeader(result.Info, field, value);
                }
            }
        }

        return result;
    }

    private static void ApplyHeader(CabrilloContestInfo info, string field, string value)
    {
        switch (field)
        {
            case "CALLSIGN": info.Callsign = value; break;
            case "CONTEST": info.Contest = value; break;
            case "CATEGORY-OPERATOR": info.CategoryOperator = value; break;
            case "CATEGORY-ASSISTED": info.CategoryAssisted = value; break;
            case "CATEGORY-BAND": info.CategoryBand = value; break;
            case "CATEGORY-MODE": info.CategoryMode = value; break;
            case "CATEGORY-POWER": info.CategoryPower = value; break;
            case "CATEGORY-STATION": info.CategoryStation = value; break;
            case "CATEGORY-TRANSMITTER": info.CategoryTransmitter = value; break;
            case "CATEGORY-OVERLAY": info.CategoryOverlay = value; break;
            case "CLAIMED-SCORE": info.ClaimedScore = value; break;
            case "CLUB": info.Club = value; break;
            case "LOCATION": info.Location = value; break;
            case "NAME": info.Name = value; break;
            case "ADDRESS": info.Address = value; break;
            case "ADDRESS-CITY": info.AddressCity = value; break;
            case "ADDRESS-STATE-PROVINCE": info.AddressStateProvince = value; break;
            case "ADDRESS-POSTALCODE": info.AddressPostalCode = value; break;
            case "ADDRESS-COUNTRY": info.AddressCountry = value; break;
            case "OPERATORS": info.Operators = value; break;
            case "EMAIL": info.Email = value; break;
            case "SOAPBOX": info.SoapBox = string.IsNullOrEmpty(info.SoapBox) ? value : info.SoapBox + "\n" + value; break;
        }
    }

    private static Qso? ParseQsoLine(string line)
    {
        // Remove "QSO:" prefix and split on whitespace.
        string body = line.Substring(4).Trim();
        var tokens = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 10) return null;

        // tokens[0] = freq (kHz), [1] = mode, [2] = date, [3] = time, [4] = sent_call,
        // [5] = sent_rst, [6] = sent_exch, [7] = rcvd_call, [8] = rcvd_rst, [9] = rcvd_exch
        if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int freqKhz))
            return null;

        string mode = MapMode(tokens[1]);
        if (!DateTime.TryParseExact(tokens[2] + " " + tokens[3], "yyyy-MM-dd HHmm",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime dt))
            return null;

        return new Qso
        {
            QsoDateTimeOnUtc = dt,
            FrequencyMhz = freqKhz / 1000m,
            Band = KhzToBand(freqKhz),
            Mode = mode,
            Callsign = tokens[7].ToUpperInvariant(),
            RstSent = tokens[5],
            RstRcvd = tokens[8],
            StxSerial = int.TryParse(tokens[6], out int stx) ? stx : null,
            SrxSerial = int.TryParse(tokens[9], out int srx) ? srx : null,
        };
    }

    private static string MapMode(string mode) => mode?.ToUpperInvariant() switch
    {
        "CW" => "CW",
        "PH" => "SSB",
        "RY" => "RTTY",
        "DG" or "FT8" => "FT8",
        _ => mode ?? string.Empty
    };

    private static string KhzToBand(int freqKhz) => freqKhz switch
    {
        >= 1800 and <= 2000 => "160m",
        >= 3500 and <= 4000 => "80m",
        >= 5330 and <= 5410 => "60m",
        >= 7000 and <= 7300 => "40m",
        >= 10100 and <= 10150 => "30m",
        >= 14000 and <= 14350 => "20m",
        >= 18068 and <= 18168 => "17m",
        >= 21000 and <= 21450 => "15m",
        >= 24890 and <= 24990 => "12m",
        >= 28000 and <= 29700 => "10m",
        >= 50000 and <= 54000 => "6m",
        >= 144000 and <= 148000 => "2m",
        >= 222000 and <= 225000 => "1.25m",
        >= 420000 and <= 450000 => "70cm",
        _ => string.Empty
    };
}
