using System.Windows;
using System.Windows.Controls;

namespace CvarcLogger.App.Views;

public partial class ContestsMenuWindow : Window
{
    // Guards against the sidebar's default-checked RadioButton (FieldDayNavItem, IsChecked="True" in
    // XAML) firing its Checked event during InitializeComponent and auto-opening the Field Day scorer
    // before the window has even finished loading. Only clicks after that point should auto-open.
    private bool _isLoaded;

    public ContestsMenuWindow()
    {
        InitializeComponent();
        _isLoaded = true;
    }

    private void ContestNav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tagValue } || !int.TryParse(tagValue, out int index))
            return;

        ContestTabControl.SelectedIndex = index;

        if (!_isLoaded)
            return;

        switch (index)
        {
            case 0:
                OpenFieldDayScorer();
                break;
            case 1:
                OpenSweepstakesScorer();
                break;
        }
    }

    private void FieldDay_Click(object sender, RoutedEventArgs e) => OpenFieldDayScorer();

    private void Sweepstakes_Click(object sender, RoutedEventArgs e) => OpenSweepstakesScorer();

    private void OpenFieldDayScorer()
    {
        var window = new FieldDayScoringWindow { Owner = this };
        window.Show();
    }

    private void OpenSweepstakesScorer()
    {
        var window = new SweepstakesScoringWindow { Owner = this };
        window.Show();
    }
}
