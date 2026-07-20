namespace CvarcLogger.Core.Models;

/// <summary>A SOTA summit being tracked toward the "Mountain Goat" activator award (1000 lifetime
/// activator points). Points is resolved from the official SOTA summit list at add-time, not
/// hand-entered.</summary>
public class SotaActivation
{
    public int Id { get; set; }

    public string SummitCode { get; set; } = string.Empty;
    public string SummitName { get; set; } = string.Empty;
    public int Points { get; set; }

    public bool Activated { get; set; }
    public DateTime? ActivationDateUtc { get; set; }
}
