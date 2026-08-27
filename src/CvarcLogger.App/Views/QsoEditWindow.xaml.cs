using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CvarcLogger.App.ViewModels;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.Views;

public partial class QsoEditWindow : Window
{
    private readonly QsoEditViewModel _viewModel;

    public QsoEditWindow(QsoEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        // DEBUG: Manually set the Lookup button command if XAML binding fails
        Loaded += (_, _) =>
        {
            var lookupButton = FindName("LookupButton") as Button;
            if (lookupButton != null && _viewModel.GetType().GetProperty("LookupCommand") is var prop && prop != null)
            {
                var cmd = prop.GetValue(_viewModel);
                if (cmd != null)
                    lookupButton.Command = (ICommand)cmd;
            }
        };
    }

    public void LoadQso(Qso qso) => _viewModel.Load(qso);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void ClearLookup_Click(object sender, RoutedEventArgs e)
    {
        // Clear lookup-related fields so Lookup will populate them fresh
        _viewModel.Name = null;
        _viewModel.GridSquare = null;
        _viewModel.Country = null;
        _viewModel.State = null;
        _viewModel.County = null;
        _viewModel.City = null;
        _viewModel.ArrlSection = null;
        _viewModel.CqZone = null;
        _viewModel.ItuZone = null;
        _viewModel.Continent = null;

        // Now run the lookup
        if (_viewModel.LookupCommand?.CanExecute(null) == true)
            await _viewModel.LookupCommand.ExecuteAsync(null);
    }
}
