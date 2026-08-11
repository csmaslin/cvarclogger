using System.ComponentModel.DataAnnotations.Schema;

namespace CvarcLogger.Core.Models;

/// <summary>A single logged contact (QSO). Field names mirror ADIF tags where a direct mapping exists.</summary>
public class Qso
{
    public int Id { get; set; }

    public string Callsign { get; set; } = string.Empty;

    public DateTime QsoDateTimeOnUtc { get; set; }
    public DateTime? QsoDateTimeOffUtc { get; set; }

    public string Band { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string? SubMode { get; set; }

    public decimal? FrequencyMhz { get; set; }
    public decimal? FrequencyRxMhz { get; set; }

    public string? RstSent { get; set; }
    public string? RstRcvd { get; set; }

    public string? Name { get; set; }
    public string? GridSquare { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ArrlSection { get; set; }

    public int? DxccEntityCode { get; set; }
    public DxccEntity? DxccEntity { get; set; }
    /// <summary>True once the user has manually corrected the auto-resolved DXCC entity; protects it from being overwritten by a future bulk re-resolution pass.</summary>
    public bool DxccEntityOverride { get; set; }

    public string? Continent { get; set; }
    public int? CqZone { get; set; }
    public int? ItuZone { get; set; }

    public decimal? TxPowerWatts { get; set; }

    public QslStatus QslSent { get; set; } = QslStatus.NotSent;
    public QslStatus QslRcvd { get; set; } = QslStatus.NotSent;
    public DateTime? QslSentDate { get; set; }
    public DateTime? QslRcvdDate { get; set; }

    public QslStatus LotwQslSent { get; set; } = QslStatus.NotSent;
    public QslStatus LotwQslRcvd { get; set; } = QslStatus.NotSent;
    public DateTime? LotwQslSentDate { get; set; }
    public DateTime? LotwQslRcvdDate { get; set; }

    public string? QslViaCallsign { get; set; }
    public string? Comment { get; set; }

    // SOTA (Summits on the Air) and POTA (Parks on the Air) activation references. ADIF field names
    // (MY_SOTA_REF/SOTA_REF, MY_SIG_INFO/SIG_INFO) kept as the property names' basis so the ADIF mapping
    // is a direct match -- see AdifFieldMapper.
    public string? MySotaRef { get; set; }
    public string? SotaRef { get; set; }
    public string? MySigInfo { get; set; }
    public string? SigInfo { get; set; }

    // Station identity — denormalized from the StationProfile in effect at save time,
    // so later edits to a profile never retroactively rewrite historical QSOs.
    public int? StationProfileId { get; set; }
    public StationProfile? StationProfile { get; set; }
    public string StationCallsign { get; set; } = string.Empty;
    public string? OperatorCallsign { get; set; }
    public string? MyGridSquare { get; set; }
    public string? MyState { get; set; }
    public string? MyCounty { get; set; }
    public string? Qth { get; set; }
    public string? Op { get; set; }

    // Local-time basis, denormalized from the StationProfile in effect at save time -- same rationale
    // as the station identity block above: editing a profile's time zone later must not retroactively
    // change how already-logged QSOs display.
    public decimal? UtcOffsetHours { get; set; }
    public bool ObservesDaylightSavingTime { get; set; }

    /// <summary>QsoDateTimeOnUtc shifted by UtcOffsetHours (plus 1h if ObservesDaylightSavingTime was
    /// set at log time). Falls back to a 0 offset for QSOs logged before this field existed. Computed,
    /// not mapped to a database column.</summary>
    public DateTime LocalDateTimeOn =>
        QsoDateTimeOnUtc.AddHours((double)(UtcOffsetHours ?? 0m) + (ObservesDaylightSavingTime ? 1 : 0));

    // Contest logging. ContestId/StxSerial/SrxSerial were reserved ahead of time; Precedence/Check/Class
    // are ARRL Sweepstakes and Field Day exchange fields, named after their exact ADIF tags.
    public string? ContestId { get; set; }
    public int? StxSerial { get; set; }
    public int? SrxSerial { get; set; }
    public string? Precedence { get; set; }
    public string? Check { get; set; }
    public string? Class { get; set; }

    // SKCC (Straight Key Century Club) membership numbers. SkccNr is the *contacted* station's number
    // (per-QSO, typed in like SotaRef/SigInfo); MySkccNr is the operator's own number, denormalized from
    // StationProfile.SkccNr at save time -- same rationale as MyGridSquare/MyState/MyCounty/Qth/Op below:
    // editing a profile's SKCC number later must not retroactively change already-logged QSOs.
    public string? SkccNr { get; set; }
    public string? MySkccNr { get; set; }

    /// <summary>JSON dictionary of any ADIF field encountered on import with no first-class column, re-emitted verbatim on export.</summary>
    public string? AdifExtraFieldsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ModifiedAtUtc { get; set; }

    /// <summary>Chronological log number shown in the log grid (oldest QSO = 1), assigned by
    /// QsoLogViewModel.RefreshAsync from the full log so it stays stable regardless of the grid's
    /// current sort/filter -- same rationale as LocalDateTimeOn above, but this one needs a setter
    /// (it depends on the other QSOs around it, not just this one's own fields), so [NotMapped] is
    /// required here to keep EF Core from trying to persist it as a real column.</summary>
    [NotMapped]
    public int LogNumber { get; set; }
}
