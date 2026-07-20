using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.Core.Awards;

namespace CvarcLogger.App.ViewModels;

public partial class AwardsViewModel : ObservableObject
{
    private readonly IAwardsService _awardsService;

    [ObservableProperty] private DxccProgress? dxccProgress;
    [ObservableProperty] private WasProgress? wasProgress;
    [ObservableProperty] private bool isLoading;

    public MountainGoatViewModel MountainGoat { get; }
    public ParksOnTheAirViewModel ParksOnTheAir { get; }

    public AwardsViewModel(IAwardsService awardsService, MountainGoatViewModel mountainGoat, ParksOnTheAirViewModel parksOnTheAir)
    {
        _awardsService = awardsService;
        MountainGoat = mountainGoat;
        ParksOnTheAir = parksOnTheAir;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            DxccProgress = await _awardsService.ComputeDxccProgressAsync();
            WasProgress = await _awardsService.ComputeWasProgressAsync();
            await MountainGoat.LoadAsync();
            await ParksOnTheAir.LoadAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
