using System.Windows;
using System.Windows.Controls;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class QsoEntryView : UserControl
{
    public QsoEntryView()
    {
        InitializeComponent();
    }

    private void LookupButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is QsoEntryViewModel viewModel)
            viewModel.LookupCommand.Execute(null);
    }

    private void LogButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is QsoEntryViewModel viewModel)
            viewModel.LogQsoCommand.Execute(null);
    }
}
