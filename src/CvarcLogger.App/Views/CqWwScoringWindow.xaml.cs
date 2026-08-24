using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CvarcLogger.App.Views;

public partial class CqWwScoringWindow : Window
{
    public CqWwScoringWindow()
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<CqWwViewModel>();
        DataContext = viewModel;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(YearTextBox.Text, out int year))
        {
            var viewModel = (CqWwViewModel)DataContext;
            await viewModel.LoadAsync(year);
        }
    }
}
