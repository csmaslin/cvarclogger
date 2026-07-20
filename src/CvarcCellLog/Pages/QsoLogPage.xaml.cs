using CvarcCellLog.ViewModels;

namespace CvarcCellLog.Pages;

public partial class QsoLogPage : ContentPage
{
    private readonly QsoLogViewModel _viewModel;

    public QsoLogPage(QsoLogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }

    private async void OnNewQsoClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(QsoEntryPage));
    }
}
