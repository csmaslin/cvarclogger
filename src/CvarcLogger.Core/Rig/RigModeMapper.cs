namespace CvarcLogger.Core.Rig;

/// <summary>Maps Hamlib/rigctld mode names to CvarcLogger's Modes list (which matches ADIF 3.1.4's
/// real Mode enumeration directly — see QsoFieldOptions). rigctld reports only the radio's demodulator
/// mode — it cannot distinguish FT8/FT4/PSK31/etc, since those are soundcard-side protocol choices the
/// radio has no visibility into. PKT* (data-on-voice-mode) therefore defaults to "FT8" -- the single
/// most common digital mode in use today, so the default is right more often than not -- rather than
/// guessing a specific digital mode; the user corrects it manually before logging if they're actually
/// running something else.</summary>
public static class RigModeMapper
{
    public static string ToCvarcLoggerMode(string rigctldMode)
    {
        string trimmed = rigctldMode.Trim();
        if (trimmed.Length == 0) return "SSB";

        return trimmed.ToUpperInvariant() switch
        {
            "USB" or "LSB" => "SSB",
            "CW" or "CWR" => "CW",
            "FM" or "WFM" => "FM",
            "AM" => "AM",
            "RTTY" or "RTTYR" => "RTTY",
            "PKTUSB" or "PKTLSB" or "PKTFM" or "PKTAM" => "FT8",
            _ => trimmed,
        };
    }

    /// <summary>Derives the SSB Sub-Mode (USB/LSB) from a raw rigctld mode string, for CAT auto-fill.
    /// rigctld reports USB/LSB as the mode itself (see ToCvarcLoggerMode), so unlike DATA sub-modes
    /// this one doesn't require the operator to specify it manually. Returns null for any non-SSB mode.</summary>
    public static string? ToCvarcLoggerSubMode(string rigctldMode) =>
        rigctldMode.Trim().ToUpperInvariant() switch
        {
            "USB" => "USB",
            "LSB" => "LSB",
            _ => null,
        };
}
