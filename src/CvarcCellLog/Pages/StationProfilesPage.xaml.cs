using CvarcCellLog.ViewModels;

namespace CvarcCellLog.Pages;

public partial class StationProfilesPage : ContentPage
{
    private readonly StationProfileViewModel _viewModel;

    public StationProfilesPage(StationProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedProfile is null) return;

        bool confirmed = await DisplayAlert("Delete Profile", $"Delete station profile {_viewModel.SelectedProfile.Callsign}? This cannot be undone.", "Delete", "Cancel");
        if (confirmed) await _viewModel.DeleteCommand.ExecuteAsync(null);
    }
}
