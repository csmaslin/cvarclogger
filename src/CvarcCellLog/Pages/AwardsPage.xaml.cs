using CvarcCellLog.ViewModels;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.Pages;

public partial class AwardsPage : ContentPage
{
    private readonly AwardsViewModel _viewModel;

    public AwardsPage(AwardsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnDeleteSummitClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: SotaActivation item }) return;

        bool confirmed = await DisplayAlert("Delete", $"Stop tracking {item.SummitCode}?", "Delete", "Cancel");
        if (confirmed) await _viewModel.MountainGoat.DeleteAsync(item);
    }

    private async void OnDeleteParkClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PotaActivation item }) return;

        bool confirmed = await DisplayAlert("Delete", $"Stop tracking {item.ParkReference}?", "Delete", "Cancel");
        if (confirmed) await _viewModel.ParksOnTheAir.DeleteAsync(item);
    }
}
