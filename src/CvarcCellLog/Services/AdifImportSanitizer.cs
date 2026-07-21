using System.Text;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.Services;

/// <summary>Drops non-ASCII characters from a freshly-imported Qso's free-text fields. AdifReader's
/// lenient decoder recovers Western-European accented Latin text but turns genuinely non-Latin
/// encodings (Korean, Japanese, etc.) into mojibake -- rather than trying to detect/decode those
/// correctly, the user asked for the simpler rule: strip non-English characters on import.</summary>
public static class AdifImportSanitizer
{
    public static void Sanitize(Qso qso)
    {
        qso.Callsign = StripNonAscii(qso.Callsign) ?? string.Empty;
        qso.SubMode = StripNonAscii(qso.SubMode);
        qso.RstSent = StripNonAscii(qso.RstSent);
        qso.RstRcvd = StripNonAscii(qso.RstRcvd);
        qso.Name = StripNonAscii(qso.Name);
        qso.GridSquare = StripNonAscii(qso.GridSquare);
        qso.City = StripNonAscii(qso.City);
        qso.County = StripNonAscii(qso.County);
        qso.State = StripNonAscii(qso.State);
        qso.Country = StripNonAscii(qso.Country);
        qso.ArrlSection = StripNonAscii(qso.ArrlSection);
        qso.QslViaCallsign = StripNonAscii(qso.QslViaCallsign);
        qso.Comment = StripNonAscii(qso.Comment);
        qso.MySotaRef = StripNonAscii(qso.MySotaRef);
        qso.SotaRef = StripNonAscii(qso.SotaRef);
        qso.MySigInfo = StripNonAscii(qso.MySigInfo);
        qso.SigInfo = StripNonAscii(qso.SigInfo);
        qso.StationCallsign = StripNonAscii(qso.StationCallsign) ?? string.Empty;
        qso.OperatorCallsign = StripNonAscii(qso.OperatorCallsign);
        qso.MyGridSquare = StripNonAscii(qso.MyGridSquare);
        qso.MyState = StripNonAscii(qso.MyState);
        qso.MyCounty = StripNonAscii(qso.MyCounty);
        qso.Qth = StripNonAscii(qso.Qth);
        qso.Op = StripNonAscii(qso.Op);
    }

    private static string? StripNonAscii(string? value)
    {
        if (value is null) return null;

        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (c < 128) builder.Append(c);
        }

        return builder.ToString().Trim();
    }
}
