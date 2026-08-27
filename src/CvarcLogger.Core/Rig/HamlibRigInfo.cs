namespace CvarcLogger.Core.Rig;

/// <summary>One entry from `rigctld --list` — a rig Hamlib knows how to drive.</summary>
public record HamlibRigInfo(int Id, string Manufacturer, string Model, string Status)
{
    /// <summary>The placeholder entry for "no radio selected" in this slot. Id 0 matches
    /// RadioProfile.HamlibModelId's existing "0 means not yet configured" convention -- Hamlib itself
    /// never assigns 0 to a real rig, so there's no collision with an actual model ID.</summary>
    public static HamlibRigInfo None { get; } = new(0, "-none-", "", "");

    public string DisplayName => Id == 0
        ? Manufacturer
        : string.IsNullOrWhiteSpace(Model)
            ? $"{Manufacturer} ({Id})"
            : $"{Manufacturer} {Model} ({Id})";
}
