namespace CvarcCellLog.ViewModels;

/// <summary>Composes the two Awards sections (Mountain Goat / SOTA and Parks on the Air / POTA) --
/// mirrors the WPF app's AwardsViewModel, which composes the same two child ViewModels for its
/// AwardsWindow's two tabs. Not itself an ObservableObject: it exposes nothing that changes after
/// construction, only the two child VMs (which are).</summary>
public class AwardsViewModel
{
    public MountainGoatViewModel MountainGoat { get; }
    public ParksOnTheAirViewModel ParksOnTheAir { get; }

    public AwardsViewModel(MountainGoatViewModel mountainGoat, ParksOnTheAirViewModel parksOnTheAir)
    {
        MountainGoat = mountainGoat;
        ParksOnTheAir = parksOnTheAir;
    }

    public async Task LoadAsync()
    {
        await MountainGoat.LoadCommand.ExecuteAsync(null);
        await ParksOnTheAir.LoadCommand.ExecuteAsync(null);
    }
}
