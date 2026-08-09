using System;
using System.Collections.Generic;

namespace CvarcLogger.Data.Models;

public partial class Qso
{
    public int Id { get; set; }

    public string Callsign { get; set; } = null!;

    public DateTime QsoDateTimeOnUtc { get; set; }

    public string? QsoDateTimeOffUtc { get; set; }

    public string Band { get; set; } = null!;

    public string Mode { get; set; } = null!;

    public string? SubMode { get; set; }

    public decimal? FrequencyMhz { get; set; }

    public string? FrequencyRxMhz { get; set; }

    public string? RstSent { get; set; }

    public string? RstRcvd { get; set; }

    public string? Name { get; set; }

    public string? GridSquare { get; set; }

    public string? County { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public int? DxccEntityCode { get; set; }

    public int DxccEntityOverride { get; set; }

    public string? Continent { get; set; }

    public int? CqZone { get; set; }

    public int? ItuZone { get; set; }

    public string? TxPowerWatts { get; set; }

    public string QslSent { get; set; } = null!;

    public string QslRcvd { get; set; } = null!;

    public string? QslSentDate { get; set; }

    public string? QslRcvdDate { get; set; }

    public string LotwQslSent { get; set; } = null!;

    public string LotwQslRcvd { get; set; } = null!;

    public string? LotwQslSentDate { get; set; }

    public string? LotwQslRcvdDate { get; set; }

    public string? QslViaCallsign { get; set; }

    public string? Comment { get; set; }

    public string? Notes { get; set; }

    public int? StationProfileId { get; set; }

    public string StationCallsign { get; set; } = null!;

    public string? OperatorCallsign { get; set; }

    public string? MyGridSquare { get; set; }

    public string? MyState { get; set; }

    public string? MyCounty { get; set; }

    public string? ContestId { get; set; }

    public int? StxSerial { get; set; }

    public int? SrxSerial { get; set; }

    public string? AdifExtraFieldsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ModifiedAtUtc { get; set; }

    public string? City { get; set; }

    public virtual DxccEntity? DxccEntityCodeNavigation { get; set; }

    public virtual StationProfile? StationProfile { get; set; }
}
