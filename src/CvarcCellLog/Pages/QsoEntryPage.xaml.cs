using CvarcCellLog.ViewModels;

namespace CvarcCellLog.Pages;

public partial class QsoEntryPage : ContentPage
{
    private readonly QsoEntryViewModel _viewModel;

    public QsoEntryPage(QsoEntryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.QsoLogged += OnQsoLogged;
    }

    private async void OnQsoLogged(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Re-run every time the page appears (not just once in the constructor) so returning from
        // "Manage Station Profiles" picks up any profile that was just added/edited/deleted.
        await _viewModel.InitializeAsync();
    }

    private async void OnManageProfilesClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(StationProfilesPage));
}
