using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CvarcLogger.App.Views;

public partial class SweepstakesScoringWindow : Window
{
    public SweepstakesScoringWindow()
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<SweepstakesViewModel>();
        DataContext = viewModel;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(YearTextBox.Text, out int year))
        {
            var viewModel = (SweepstakesViewModel)DataContext;
            await viewModel.LoadAsync(year);
        }
    }
}
