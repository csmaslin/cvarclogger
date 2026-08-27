using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.Core.Awards;

namespace CvarcLogger.App.ViewModels;

public partial class AwardsViewModel : ObservableObject
{
    private readonly IAwardsService _awardsService;

    [ObservableProperty] private WasProgress? wasProgress;
    [ObservableProperty] private bool isLoading;

    public DxccViewModel Dxcc { get; }
    public MountainGoatViewModel MountainGoat { get; }
    public ParksOnTheAirViewModel ParksOnTheAir { get; }
    public SkccViewModel Skcc { get; }

    public AwardsViewModel(IAwardsService awardsService, DxccViewModel dxcc, MountainGoatViewModel mountainGoat, ParksOnTheAirViewModel parksOnTheAir, SkccViewModel skcc)
    {
        _awardsService = awardsService;
        Dxcc = dxcc;
        MountainGoat = mountainGoat;
        ParksOnTheAir = parksOnTheAir;
        Skcc = skcc;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await Dxcc.LoadAsync();
            WasProgress = await _awardsService.ComputeWasProgressAsync();
            await MountainGoat.LoadAsync();
            await ParksOnTheAir.LoadAsync();
            await Skcc.LoadAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
