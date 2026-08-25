namespace CvarcLogger.Core.Models;

/// <summary>SKCC (Straight Key Century Club) member record from the club's master member list.
/// Used for live callsign lookup during contest entry to auto-populate operator name, QTH, and member number.</summary>
public class SkccMember
{
    public int Id { get; set; }
    public required string Callsign { get; set; }      // "W5ABC"
    public string? MemberNumber { get; set; }          // "1234" or "1234S" (with tier suffix)
    public string? Name { get; set; }                  // "Pete"
    public string? Qth { get; set; }                   // "CA" (US/Canada) or SPC code (international)
    public char? MemberStatus { get; set; }            // 'C' (Centurion), 'T' (Tribune), 'S' (Senator), or null
    public bool Active { get; set; }
    public DateTime LastUpdated { get; set; }          // When this record was last fetched from SKCC master list
}
