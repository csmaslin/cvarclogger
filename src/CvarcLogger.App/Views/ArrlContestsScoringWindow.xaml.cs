using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CvarcLogger.App.Views;

public partial class ArrlContestsScoringWindow : Window
{
    public ArrlContestsScoringWindow()
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<ArrlContestsViewModel>();
        DataContext = viewModel;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(YearTextBox.Text, out int year))
        {
            var viewModel = (ArrlContestsViewModel)DataContext;
            await viewModel.LoadAsync(year);
        }
    }
}
