using System.Windows;
using CvarcLogger.App.ViewModels;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.Views;

public partial class BulkEditWindow : Window
{
    private readonly BulkEditViewModel _viewModel;

    public BulkEditWindow(BulkEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public void LoadQsos(IReadOnlyList<Qso> qsos) => _viewModel.Load(qsos);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
