namespace CvarcLogger.Core.Models;

/// <summary>A callsign/identity the user operates under (e.g. personal call vs. an ARES/club call). Selected during QSO entry and denormalized onto each Qso.</summary>
public class StationProfile
{
    public int Id { get; set; }

    public string Callsign { get; set; } = string.Empty;
    public string? OperatorCallsign { get; set; }
    public string? MyGridSquare { get; set; }
    public string? MyState { get; set; }
    public string? MyCounty { get; set; }

    public bool IsDefault { get; set; }
    public string? Notes { get; set; }
}
