using CvarcCellLog.ViewModels;

namespace CvarcCellLog.Pages;

public partial class QsoEditPage : ContentPage
{
    private readonly QsoEditViewModel _viewModel;

    public QsoEditPage(QsoEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.Saved += OnSavedOrDeleted;
        _viewModel.Deleted += OnSavedOrDeleted;
    }

    private async void OnSavedOrDeleted(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        bool confirmed = await DisplayAlert("Delete QSO", $"Delete the QSO with {_viewModel.Callsign}? This cannot be undone.", "Delete", "Cancel");
        if (confirmed) await _viewModel.DeleteCommand.ExecuteAsync(null);
    }
}
