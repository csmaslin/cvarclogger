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
    // Hoisted separators to avoid allocating a fresh array on every ParseQsoLine / ApplyExchange call.
    private static readonly char[] WhitespaceSeparators = { ' ', '\t' };
    private static readonly char[] SpaceSeparator = { ' ' };

    public class ParseResult
    {
        public CabrilloContestInfo Info { get; set; } = new();
        public List<Qso> Qsos { get; set; } = new();
    }

    public static ParseResult ReadAll(string filePath)
    {
        var result = new ParseResult();

        // File.ReadLines streams the file rather than loading every line into a string[] up front
        // (File.ReadAllLines), which matters for very large contest logs. We also break on END-OF-LOG
        // so the rest of the file is skipped entirely, not just parsed-and-ignored.
        foreach (var line in File.ReadLines(filePath))
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
        // Remove "QSO:" prefix and split on whitespace using the hoisted separator.
        string body = line.Substring(4).Trim();
        var tokens = body.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 10) return null;

        // tokens[0] = freq (kHz), [1] = mode, [2] = date, [3] = time, [4] = sent_call,
        // [5] = sent_rst, [6] = sent_exch, [7] = rcvd_call, [8] = rcvd_rst, [9] = rcvd_exch
        // Some contests split the exchange across multiple tokens (e.g. "3A CO" for Field Day) --
        // gather everything past index 9 into the received exchange, and everything from 6 to end-of-
        // rcvd_call for the sent exchange.
        if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int freqKhz))
            return null;

        string mode = MapMode(tokens[1]);
        if (!DateTime.TryParseExact(tokens[2] + " " + tokens[3], "yyyy-MM-dd HHmm",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime dt))
            return null;

        var qso = new Qso
        {
            QsoDateTimeOnUtc = dt,
            FrequencyMhz = freqKhz / 1000m,
            Band = KhzToBand(freqKhz),
            Mode = mode,
            StationCallsign = tokens[4].ToUpperInvariant(),
            Callsign = tokens[7].ToUpperInvariant(),
            RstSent = tokens[5],
            RstRcvd = tokens[8],
        };

        // Sent exchange: token[6] plus any extra tokens up to the receiving callsign at token[7].
        // In the standard 10-column format there is only ever one sent-exchange token, but some
        // sponsors run 11-column ("QSO: freq mode date time call rst exch1 exch2 rcvdcall rst rcvdexch")
        // Since we can't reliably distinguish, treat token[6] alone as the sent exchange.
        ApplyExchange(qso, tokens[6], sent: true);

        // Received exchange: token[9] and any trailing tokens (e.g. "3A CO" is two tokens).
        string rcvdExch = string.Join(" ", tokens.Skip(9)).Trim();
        ApplyExchange(qso, rcvdExch, sent: false);

        return qso;
    }

    /// <summary>Route a Cabrillo exchange value into the appropriate Qso field. Serial numbers go to
    /// StxSerial/SrxSerial, US state abbreviations to State, known ARRL sections to ArrlSection, and
    /// Field-Day-style "class + section" (e.g. "3A CO") to Class + State/Section. Everything else
    /// falls back to Class so no information is lost.</summary>
    private static void ApplyExchange(Qso qso, string exchange, bool sent)
    {
        if (string.IsNullOrWhiteSpace(exchange)) return;

        // Fast path: no whitespace means a single token -- skip Split's array allocation entirely.
        // This is the common case (contests where the exchange is just a serial or a state code).
        if (exchange.IndexOf(' ') < 0)
        {
            if (int.TryParse(exchange, out int singleSerial))
            {
                if (sent) qso.StxSerial = singleSerial;
                else qso.SrxSerial = singleSerial;
            }
            else
            {
                RouteLocationToken(qso, exchange, sent);
            }
            return;
        }

        var parts = exchange.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);

        // Pure numeric (also handled above, but keep for tab-separated / multi-space input)
        if (parts.Length == 1 && int.TryParse(parts[0], out int serial))
        {
            if (sent) qso.StxSerial = serial;
            else qso.SrxSerial = serial;
            return;
        }

        // Field Day / Sweepstakes pattern: "3A CO" (class + section) or "A CO" or "3 CO"
        if (parts.Length == 2 && LooksLikeContestClass(parts[0]))
        {
            if (!sent) qso.Class = parts[0].ToUpperInvariant();
            RouteLocationToken(qso, parts[1], sent);
            return;
        }

        if (parts.Length == 1)
        {
            RouteLocationToken(qso, parts[0], sent);
            return;
        }

        // Multi-token with no recognizable class prefix: keep the whole string as Class so nothing is lost.
        if (!sent) qso.Class = exchange.ToUpperInvariant();
    }

    /// <summary>A "contest class" like "3A", "5F", "1B" (number + letter) as used by Field Day, or
    /// a bare letter (Sweepstakes precedence like "A"/"B"/"U"), or a bare number of ops.</summary>
    private static bool LooksLikeContestClass(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        // Number + letter, e.g. "3A", "20A"
        int i = 0;
        while (i < token.Length && char.IsDigit(token[i])) i++;
        if (i > 0 && i < token.Length && token.Substring(i).All(char.IsLetter)) return true;
        // Bare letter, e.g. "A"
        if (token.Length <= 2 && token.All(char.IsLetter) && !UsStates.Contains(token.ToUpperInvariant())
            && !ArrlSections.Contains(token.ToUpperInvariant())) return true;
        return false;
    }

    /// <summary>Store a two/three-letter location code in the best-matching field.</summary>
    private static void RouteLocationToken(Qso qso, string token, bool sent)
    {
        string upper = token.ToUpperInvariant();
        if (UsStates.Contains(upper))
        {
            if (!sent) qso.State = upper;
        }
        else if (ArrlSections.Contains(upper))
        {
            if (!sent) qso.ArrlSection = upper;
        }
        else
        {
            if (!sent) qso.Class = upper;
        }
    }

    private static readonly HashSet<string> UsStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","DC","FL","GA","HI","ID","IL","IN","IA","KS","KY",
        "LA","ME","MD","MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ","NM","NY","NC","ND","OH",
        "OK","OR","PA","RI","SC","SD","TN","TX","UT","VT","VA","WA","WV","WI","WY","PR","VI",
    };

    private static readonly HashSet<string> ArrlSections = new(StringComparer.OrdinalIgnoreCase)
    {
        // Single-section states use their state abbrev, so those are already handled by UsStates.
        // These are the SPLIT-state / Canadian / regional codes only.
        "EB","LAX","ORG","SDG","SF","SCV","SB","SJV","SV",           // CA splits
        "NLI","NNY","ENY","WNY",                                     // NY splits
        "EPA","WPA",                                                 // PA splits
        "EMA","WMA",                                                 // MA splits
        "EWA","WWA",                                                 // WA splits
        "STX","NTX","WTX","SFL","NFL","WCF",                         // TX, FL splits
        "MDC",                                                       // MD/DC
        "MAR","GTA","ONE","ONN","ONS","ONT",                         // Canada
    };

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
