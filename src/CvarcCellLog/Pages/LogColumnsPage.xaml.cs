using CvarcCellLog.ViewModels;

namespace CvarcCellLog.Pages;

public partial class LogColumnsPage : ContentPage
{
    private readonly LogColumnsViewModel _viewModel;

    public LogColumnsPage(LogColumnsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        _viewModel.Save();
        await Shell.Current.GoToAsync("..");
    }
}
