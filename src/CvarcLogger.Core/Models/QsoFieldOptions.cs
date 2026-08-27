namespace CvarcLogger.Core.Models;

/// <summary>Canonical choice lists for QSO Band/Mode/Sub-Mode pickers — shared by QsoEntryViewModel and
/// QsoEditViewModel so the two forms can't silently drift apart (e.g. a mode added to one picker but
/// not the other).</summary>
public static class QsoFieldOptions
{
    // "1.25M" (222-225 MHz) matches ADIF's actual Band enumeration token; earlier versions used the
    // wrong "1.2M" (see AdifFieldMapper.NormalizeBandForAdif, which still corrects already-logged QSOs
    // on export without a database migration).
    public static readonly IReadOnlyList<string> Bands = new[]
    {
        "160m", "80m", "60m", "40m", "30m", "20m", "17m", "15m", "12m", "10m", "6m", "2m", "1.25M", "70cm"
    };

    // Modes match ADIF 3.1.4's real top-level Mode enumeration directly -- no more synthetic "DATA"
    // bucket (never a valid ADIF Mode). FT8/FT4/RTTY have no ADIF Sub-Mode; PSK and DIGITALVOICE do
    // (PskSubModes/DigitalVoiceSubModes below) -- see AdifFieldMapper, which now round-trips Mode/
    // SubMode as a plain passthrough since these values already match ADIF's own vocabulary.
    public static readonly IReadOnlyList<string> Modes = new[]
    {
        "SSB", "CW", "FM", "AM", "FT8", "FT4", "RTTY", "PSK", "DIGITALVOICE"
    };

    /// <summary>Sub-Mode picker choices while Mode is "PSK" -- ADIF's PSK31 is Mode=PSK, SubMode=PSK31,
    /// not its own top-level mode. See SubModeVisibilityConverter.</summary>
    public static readonly IReadOnlyList<string> PskSubModes = new[]
    {
        "PSK31"
    };

    /// <summary>Sub-Mode picker choices while Mode is "DIGITALVOICE" -- ADIF has no standalone "DMR" or
    /// "D-STAR" Mode; both are Sub-Modes of DIGITALVOICE. rigctld has no visibility into which digital
    /// voice protocol is running (see RigModeMapper), so the operator picks it manually. "DSTAR" (no
    /// hyphen) matches ADIF's exact SubMode token.</summary>
    public static readonly IReadOnlyList<string> DigitalVoiceSubModes = new[]
    {
        "DMR", "DSTAR"
    };

    /// <summary>Sub-Mode picker choices while Mode is "SSB" -- unlike PSK/DIGITALVOICE sub-modes, CAT
    /// auto-fill can discover these directly from the radio (rigctld reports USB/LSB as the mode
    /// itself), see RigModeMapper.ToCvarcLoggerSubMode.</summary>
    public static readonly IReadOnlyList<string> SsbSubModes = new[]
    {
        "USB", "LSB"
    };
}
