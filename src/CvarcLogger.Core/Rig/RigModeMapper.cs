namespace CvarcLogger.Core.Rig;

/// <summary>Maps Hamlib/rigctld mode names to CvarcLogger's Modes list. rigctld reports only the
/// radio's demodulator mode — it cannot distinguish FT8/FT4/PSK31/etc, since those are soundcard-
/// side protocol choices the radio has no visibility into. PKT* (data-on-voice-mode) therefore maps
/// to the generic "DATA" entry rather than guessing a specific digital mode; the user corrects it
/// manually before logging if they know which digital mode they're actually running.</summary>
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
            "PKTUSB" or "PKTLSB" or "PKTFM" or "PKTAM" => "DATA",
            _ => trimmed,
        };
    }
}
