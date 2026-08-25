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

        // FieldDayNavItem's IsChecked="True" in XAML fires its Checked event during InitializeComponent,
        // before ContestTabControl is necessarily ready to receive a SelectedIndex -- and because
        // RadioButtons only raise Checked on an unchecked->checked transition, a user's first click on
        // an already-checked FieldDayNavItem does nothing at all, leaving the content area blank until
        // some other tab is clicked first (a real transition). Setting SelectedIndex explicitly here,
        // after InitializeComponent has fully finished, guarantees the default tab's content actually
        // shows without depending on that XAML-time event's timing.
        ContestTabControl.SelectedIndex = 0;

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
            case 2:
                OpenCqWwScorer();
                break;
            case 3:
                OpenSprintsScorer();
                break;
            case 4:
                OpenNaqpScorer();
                break;
            case 5:
                OpenArrlContestsScorer();
                break;
        }
    }

    private void FieldDay_Click(object sender, RoutedEventArgs e) => OpenFieldDayScorer();

    private void Sweepstakes_Click(object sender, RoutedEventArgs e) => OpenSweepstakesScorer();

    private void CqWw_Click(object sender, RoutedEventArgs e) => OpenCqWwScorer();

    private void Sprints_Click(object sender, RoutedEventArgs e) => OpenSprintsScorer();

    private void Naqp_Click(object sender, RoutedEventArgs e) => OpenNaqpScorer();

    private void ArrlContests_Click(object sender, RoutedEventArgs e) => OpenArrlContestsScorer();

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

    private void OpenCqWwScorer()
    {
        var window = new CqWwScoringWindow { Owner = this };
        window.Show();
    }

    private void OpenSprintsScorer()
    {
        var window = new SprintsScoringWindow { Owner = this };
        window.Show();
    }

    private void OpenNaqpScorer()
    {
        var window = new NaqpScoringWindow { Owner = this };
        window.Show();
    }

    private void OpenArrlContestsScorer()
    {
        var window = new ArrlContestsScoringWindow { Owner = this };
        window.Show();
    }
}
