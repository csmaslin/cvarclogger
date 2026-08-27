namespace CvarcLogger.Core.Models;

/// <summary>A saved Cabrillo header describing one contest submission (or an imported log's header).
/// Populated from the Cabrillo Export dialog on export, or from the header of an imported .cbr file.
/// QSOs are matched back to a submission at export time by ContestId + date range -- not by a
/// foreign key on Qso -- so a QSO can be re-exported under a different submission without needing
/// a schema change on Qso itself.</summary>
public class ContestSubmission
{
    public int Id { get; set; }

    /// <summary>Cabrillo CONTEST field, e.g. "ARRL-DX-CW", "CQ-WW-SSB". Denormalized onto each Qso's
    /// ContestId column at import time so re-export can find the matching submission.</summary>
    public string ContestId { get; set; } = string.Empty;

    /// <summary>Cabrillo CALLSIGN field -- the callsign this entry was submitted under. Distinct from
    /// per-QSO StationCallsign in multi-op or guest-op setups where the entry callsign differs from the
    /// individual operator's own call.</summary>
    public string Callsign { get; set; } = string.Empty;

    // Category fields -- see CabrilloContestInfo for what each one means.
    public string CategoryOperator { get; set; } = "SINGLE-OP";
    public string CategoryAssisted { get; set; } = "NON-ASSISTED";
    public string CategoryBand { get; set; } = "ALL";
    public string CategoryMode { get; set; } = "MIXED";
    public string CategoryPower { get; set; } = "LOW";
    public string CategoryStation { get; set; } = "FIXED";
    public string CategoryTransmitter { get; set; } = "ONE";
    public string CategoryOverlay { get; set; } = string.Empty;

    public string ClaimedScore { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    // Submitter contact info -- kept on the submission rather than the operator's station profile
    // because a club station may submit multiple contest entries under different mailing addresses
    // (Field Day site vs. clubhouse etc.).
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string AddressCity { get; set; } = string.Empty;
    public string AddressStateProvince { get; set; } = string.Empty;
    public string AddressPostalCode { get; set; } = string.Empty;
    public string AddressCountry { get; set; } = string.Empty;

    /// <summary>Space-separated list of operator callsigns for MULTI-OP entries. Freeform text as it
    /// appears in the Cabrillo file, not a relational link into StationProfiles.</summary>
    public string Operators { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Cabrillo SOAPBOX free-text comments -- station description, antenna setup, thanks etc.
    /// Sponsors publish these in results write-ups. May span multiple lines.</summary>
    public string SoapBox { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ModifiedAtUtc { get; set; }
}
