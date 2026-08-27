using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CvarcLogger.App.ViewModels;
using CvarcLogger.Core.Models;

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

    private void MountainGoatGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is not SotaActivation item) return;

        // CellEditEnding fires just before the binding source is actually updated, so defer the
        // save to let the edited value land on the model first. SotaActivation isn't observable, so
        // the Points column's Activated-driven MultiBinding (plain "12" vs. "(12)") won't repaint on
        // its own -- Items.Refresh() forces it after toggling the Activated checkbox.
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            await _viewModel.MountainGoat.SaveRowAsync(item);
            MountainGoatGrid.Items.Refresh();
        }), DispatcherPriority.Background);
    }

    private void PotaGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is not PotaActivation item) return;

        // CellEditEnding fires just before the binding source is actually updated, so defer the
        // save to let the edited value land on the model first.
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            await _viewModel.ParksOnTheAir.SaveRowAsync(item);
            PotaGrid.Items.Refresh();
        }), DispatcherPriority.Background);
    }
}
