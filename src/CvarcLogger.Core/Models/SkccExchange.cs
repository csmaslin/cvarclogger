namespace CvarcLogger.Core.Models;

/// <summary>Parsed SKCC contest exchange data from a single QSO. Used during entry validation
/// and scoring to extract member status, multiplier value, and verify required fields.</summary>
public class SkccExchange
{
    public required string Callsign { get; set; }
    public string? RstSent { get; set; }               // "599", "579"
    public string? RstReceived { get; set; }
    public string? Qth { get; set; }                   // "CA" or SPC code
    public string? OperatorName { get; set; }          // "PETE"
    public string? MemberNumber { get; set; }          // "1234", "1234S", "NONE", or empty
    public string? GridSquare { get; set; }            // "DM04" (QSO Party only)
    public char? MemberStatus { get; set; }            // Parsed from "1234S" → 'S', or null

    public List<string> ValidationErrors { get; set; } = new();
    public bool IsValid => !ValidationErrors.Any();
}
