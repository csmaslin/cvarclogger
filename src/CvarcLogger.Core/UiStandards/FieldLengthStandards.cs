namespace CvarcLogger.Core.UiStandards;

/// <summary>Shared QSO-entry/edit and station-profile field-length standards for both CvarcLogger (WPF)
/// and CvarcCellLog (MAUI), so their input boxes stay consistent with each other. Each app derives its
/// own on-screen box size from these character counts using its own framework's sizing conventions --
/// this only defines the character-count ceiling, not a pixel width. Set directly by the user
/// (csmaslin) on 2026-07-28; change here and both apps pick up the new value on their next build.</summary>
public static class FieldLengthStandards
{
    public const int NameMaxLength = 20;
    public const int GridSquareMaxLength = 6;
    public const int CountryMaxLength = 20;
    public const int CallsignMaxLength = 10;
    public const int BandMaxLength = 4;
    public const int ModeMaxLength = 4;
    public const int SubModeMaxLength = 4;

    /// <summary>RST Sent/Rcvd. Content rule (not yet enforced in code, box size only for now): first
    /// character numeric 1-5, second and third characters each either numeric 1-5 or "N".</summary>
    public const int RstMaxLength = 3;

    public const int StateMaxLength = 6;
    public const int ArrlSectionMaxLength = 3;

    /// <summary>Numeric only (not yet enforced in code, box size only for now).</summary>
    public const int CqZoneMaxLength = 2;

    /// <summary>Numeric only (not yet enforced in code, box size only for now).</summary>
    public const int ItuZoneMaxLength = 2;

    public const int SotaRefMaxLength = 22;
    public const int PotaRefMaxLength = 22;

    /// <summary>Numeric only (not yet enforced in code, box size only for now).</summary>
    public const int TxPowerMaxLength = 5;

    public const int CityMaxLength = 20;
    public const int CountyMaxLength = 20;
    public const int QthMaxLength = 10;
    public const int OpMaxLength = 10;
    public const int CommentMaxLength = 25;

    /// <summary>Frequency (MHz). Numeric only (not yet enforced in code, box size only for now). Sized
    /// for up through microwave bands, e.g. "1296.000".</summary>
    public const int FrequencyMaxLength = 8;

    /// <summary>Date/Time (On UTC, On Local, Off UTC, Off Local). Matches the fixed "yyyy-MM-dd
    /// HH:mm:ss" format used for every editable date/time field in both apps -- exactly 19 characters,
    /// unlike a locale-formatted display string (e.g. .NET's "g" format), which varies in length.</summary>
    public const int DateTimeMaxLength = 19;

    /// <summary>ARRL Sweepstakes precedence. Restricted to the ARRL-defined value set: Q/A/B/U/M/S.</summary>
    public const int PrecedenceMaxLength = 1;

    /// <summary>ARRL Sweepstakes check -- the 2-digit year the operator was first licensed. Freeform,
    /// no fixed value set.</summary>
    public const int CheckMaxLength = 2;

    /// <summary>ARRL Field Day class, e.g. "3A", "10D". Freeform (the transmitter-count portion isn't
    /// bounded by a fixed value set), just the category letter is.</summary>
    public const int ClassMaxLength = 4;

    /// <summary>SKCC (Straight Key Century Club) membership number, e.g. "12345C". Shared by
    /// StationProfile.SkccNr (the operator's own, fixed) and Qso.SkccNr (the contacted station's,
    /// per-QSO).</summary>
    public const int SkccNrMaxLength = 10;

    /// <summary>Contest exchange serial number sent (Qso.StxSerial), e.g. "0042". 4 digits covers any
    /// realistic single-operator contest run.</summary>
    public const int SequenceNumberMaxLength = 4;
}
