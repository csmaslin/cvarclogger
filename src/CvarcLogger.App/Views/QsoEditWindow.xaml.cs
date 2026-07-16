using System.Windows;
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
    }

    public void LoadQso(Qso qso) => _viewModel.Load(qso);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
