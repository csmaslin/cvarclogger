using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CvarcLogger.App.Views;

public partial class NaqpScoringWindow : Window
{
    public NaqpScoringWindow()
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<NaqpViewModel>();
        DataContext = viewModel;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(YearTextBox.Text, out int year))
        {
            var viewModel = (NaqpViewModel)DataContext;
            await viewModel.LoadAsync(year);
        }
    }
}
