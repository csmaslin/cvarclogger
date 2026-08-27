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

    private async void BackfillButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(YearTextBox.Text, out int year))
        {
            MessageBox.Show(this, "Enter a valid year (e.g. 2025).", "ARRL Sweepstakes",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var viewModel = (SweepstakesViewModel)DataContext;
        var result = await viewModel.BackfillArrlSectionsAsync(year);

        MessageBox.Show(this,
            $"Backfill complete for {year} CW/Phone windows:\n\n" +
            $"Sections filled in: {result.Updated}\n" +
            $"Already had a section: {result.AlreadyPresent}\n" +
            $"Skipped (no State recorded): {result.SkippedNoState}\n" +
            $"Skipped (State/County not in resolver's table): {result.SkippedUnresolved}",
            "ARRL Sweepstakes", MessageBoxButton.OK, MessageBoxImage.Information);

        await viewModel.LoadAsync(year);
    }
}
