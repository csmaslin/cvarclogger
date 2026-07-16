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

    public AwardsViewModel(IAwardsService awardsService)
    {
        _awardsService = awardsService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            DxccProgress = await _awardsService.ComputeDxccProgressAsync();
            WasProgress = await _awardsService.ComputeWasProgressAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
