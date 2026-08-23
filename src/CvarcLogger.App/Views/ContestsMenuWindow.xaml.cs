using System.Windows;

namespace CvarcLogger.App.Views;

public partial class ContestsMenuWindow : Window
{
    public ContestsMenuWindow()
    {
        InitializeComponent();
    }

    private void FieldDay_Click(object sender, RoutedEventArgs e)
    {
        var window = new FieldDayScoringWindow { Owner = this };
        window.Show();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
