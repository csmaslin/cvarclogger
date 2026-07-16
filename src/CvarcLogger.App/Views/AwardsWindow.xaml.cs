using System.Windows;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class AwardsWindow : Window
{
    private readonly AwardsViewModel _viewModel;

    public AwardsWindow(AwardsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }
}
