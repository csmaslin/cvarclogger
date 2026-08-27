namespace CvarcLogger.Core.Cabrillo;

/// <summary>Header fields for a Cabrillo v3 contest log. Filled in by the operator at export time
/// via the Cabrillo Export dialog. Only Callsign and Contest are strictly required; the rest are
/// contest-sponsor-specific and can be left blank.</summary>
public class CabrilloContestInfo
{
    public string Callsign { get; set; } = string.Empty;
    public string Contest { get; set; } = string.Empty;
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
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string AddressCity { get; set; } = string.Empty;
    public string AddressStateProvince { get; set; } = string.Empty;
    public string AddressPostalCode { get; set; } = string.Empty;
    public string AddressCountry { get; set; } = string.Empty;
    public string Operators { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SoapBox { get; set; } = string.Empty;
}
