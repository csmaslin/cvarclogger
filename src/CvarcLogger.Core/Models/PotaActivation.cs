namespace CvarcLogger.Core.Models;

/// <summary>A POTA park being tracked toward the activator award tiers (Bronze 10, Silver 20, Gold 30,
/// Platinum 40, Diamond 50, Ruby 100, Emerald 125 unique parks activated) and the separate per-park Kilo
/// award (1,000 cumulative QSOs logged at that one park). ParkName is resolved from the bundled POTA
/// park list, not hand-entered.</summary>
public class PotaActivation
{
    public int Id { get; set; }

    public string ParkReference { get; set; } = string.Empty;
    public string ParkName { get; set; } = string.Empty;

    public bool Activated { get; set; }
    public DateTime? ActivationDateUtc { get; set; }

    /// <summary>Cumulative QSO count ever logged at this park, across every activation session --
    /// unlike Activated/ActivationDateUtc, this keeps climbing even after the park is already
    /// activated, since it's what the Kilo award (1,000 QSOs at one park) tracks.</summary>
    public int TotalQsoCount { get; set; }

    public bool IsKiloEligible => TotalQsoCount >= 1000;
}
