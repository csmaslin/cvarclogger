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
}
