using System.Globalization;
using System.Linq;

namespace CvarcLogger.App.Services;

/// <summary>Parses Elecraft K4 native CAT command replies (semicolon-terminated ASCII, e.g. "FA07100000;",
/// "MD1;"). Digit-extraction is deliberately tolerant of the exact digit count rather than assuming a
/// fixed width -- the K4 Programmer's Reference's documented width for FA's reply doesn't quite match a
/// real worked example, so stripping non-digit characters and parsing what's left is safer than hardcoding
/// a count that might be wrong on real hardware.</summary>
public static class K4ReplyParser
{
    public static bool TryParseFrequencyHz(string reply, out decimal hz)
    {
        string digits = new(reply.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out hz);
    }

    public static bool TryParseModeDigit(string reply, out char digit)
    {
        digit = reply.FirstOrDefault(char.IsDigit);
        return digit != default;
    }

    /// <summary>Parses a PC reply ("PCnnnr;", e.g. "PC050H;") into watts. The trailing range letter changes
    /// what the 3-digit field means (K4 Programmer's Reference, rev. D4): 'L' (QRP, 0.1-10.0 W) and 'X'
    /// (milliwatt/XVTR range) are in tenths of the display unit, while 'H' (QRO, 1-110 W) is whole watts.
    /// If the range letter is missing (legacy K3-format reply), 'L' is assumed per the spec.</summary>
    public static bool TryParsePowerWatts(string reply, out decimal watts)
    {
        watts = 0;
        string trimmed = reply.TrimEnd(';');
        if (!trimmed.StartsWith("PC", StringComparison.OrdinalIgnoreCase)) return false;

        string body = trimmed[2..];
        char range = 'L';
        if (body.Length > 0 && char.IsLetter(body[^1]))
        {
            range = char.ToUpperInvariant(body[^1]);
            body = body[..^1];
        }

        if (!decimal.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw)) return false;

        watts = range == 'H' ? raw : raw / 10m;
        return true;
    }
}
