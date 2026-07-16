using System.Windows;

namespace CvarcLogger.App.Services;

/// <summary>Thin wrapper over MessageBox so ViewModels don't take a direct WPF dependency.</summary>
public class DialogService
{
    public void ShowInfo(string message, string title = "CvarcLogger") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message, string title = "CvarcLogger") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "CvarcLogger") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
