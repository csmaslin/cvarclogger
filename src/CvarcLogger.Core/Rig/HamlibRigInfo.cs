namespace CvarcLogger.Core.Rig;

/// <summary>One entry from `rigctld --list` — a rig Hamlib knows how to drive.</summary>
public record HamlibRigInfo(int Id, string Manufacturer, string Model, string Status)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Model)
        ? $"{Manufacturer} ({Id})"
        : $"{Manufacturer} {Model} ({Id})";
}
