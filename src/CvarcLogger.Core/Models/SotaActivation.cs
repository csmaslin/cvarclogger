namespace CvarcLogger.Core.Models;

/// <summary>A SOTA summit being tracked toward the "Mountain Goat" activator award (1000 lifetime
/// activator points). Points is resolved from the official SOTA summit list at add-time, not
/// hand-entered. ContactCount is computed from the QSO log at runtime and never persisted.</summary>
public class SotaActivation
{
    public int Id { get; set; }

    public string SummitCode { get; set; } = string.Empty;
    public string SummitName { get; set; } = string.Empty;
    public int Points { get; set; }

    public bool Activated { get; set; }
    public DateTime? ActivationDateUtc { get; set; }

    public int ContactCount { get; set; }
}
