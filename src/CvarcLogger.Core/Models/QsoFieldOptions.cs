namespace CvarcLogger.Core.Models;

/// <summary>Canonical choice lists for QSO Band/Mode/Sub-Mode pickers — shared by QsoEntryViewModel and
/// QsoEditViewModel so the two forms can't silently drift apart (e.g. a mode added to one picker but
/// not the other).</summary>
public static class QsoFieldOptions
{
    public static readonly IReadOnlyList<string> Bands = new[]
    {
        "160m", "80m", "60m", "40m", "30m", "20m", "17m", "15m", "12m", "10m", "6m", "2m", "70cm"
    };

    public static readonly IReadOnlyList<string> Modes = new[]
    {
        "SSB", "CW", "FM", "AM", "FT8", "FT4", "RTTY", "PSK31", "DMR", "D-STAR", "DATA"
    };

    /// <summary>Choices for the Sub-Mode picker, shown only while Mode is "DATA" — see
    /// DataModeVisibilityConverter for the full rationale.</summary>
    public static readonly IReadOnlyList<string> SubModes = new[]
    {
        "FT8", "FT4", "RTTY", "PSK31", "DMR", "D-STAR"
    };
}
