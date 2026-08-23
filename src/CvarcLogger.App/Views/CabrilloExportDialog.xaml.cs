using System.Windows;
using System.Windows.Controls;
using CvarcLogger.Core.Cabrillo;

namespace CvarcLogger.App.Views;

public partial class CabrilloExportDialog : Window
{
    public CabrilloContestInfo? Result { get; private set; }

    /// <summary>Called when the operator selects/types a contest name. Returns any previously-saved
    /// header for that contest so we can pre-fill the rest of the form (address, category choices,
    /// club, etc.). Passed in from the caller so this dialog stays UI-only with no data-layer coupling.</summary>
    private readonly Func<string, CancellationToken, Task<CabrilloContestInfo?>>? _loadPriorForContest;

    private bool _restoringFromPriorSubmission;

    public CabrilloExportDialog(
        CabrilloContestInfo? defaults = null,
        Func<string, CancellationToken, Task<CabrilloContestInfo?>>? loadPriorForContest = null)
    {
        InitializeComponent();

        _loadPriorForContest = loadPriorForContest;

        var info = defaults ?? new CabrilloContestInfo();
        ApplyInfoToFields(info);

        ContestBox.SelectionChanged += ContestBox_Changed;
        ContestBox.LostFocus += ContestBox_Changed;
    }

    private void ApplyInfoToFields(CabrilloContestInfo info)
    {
        CallsignBox.Text = info.Callsign;
        ContestBox.Text = info.Contest;
        CategoryOperatorBox.Text = info.CategoryOperator;
        CategoryBandBox.Text = info.CategoryBand;
        CategoryModeBox.Text = info.CategoryMode;
        CategoryPowerBox.Text = info.CategoryPower;
        CategoryStationBox.Text = info.CategoryStation;
        NameBox.Text = info.Name;
        LocationBox.Text = info.Location;
        ClaimedScoreBox.Text = info.ClaimedScore;
        EmailBox.Text = info.Email;
        SoapBoxBox.Text = info.SoapBox;
    }

    private async void ContestBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (_restoringFromPriorSubmission || _loadPriorForContest is null) return;
        string contest = ContestBox.Text?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(contest)) return;

        try
        {
            var prior = await _loadPriorForContest(contest, CancellationToken.None);
            if (prior is null) return;

            // Preserve whatever the operator has already typed into the callsign field -- switching the
            // contest shouldn't blow away a manual callsign edit.
            string callsignAlreadyTyped = CallsignBox.Text?.Trim() ?? string.Empty;

            _restoringFromPriorSubmission = true;
            ApplyInfoToFields(prior);
            if (!string.IsNullOrEmpty(callsignAlreadyTyped)) CallsignBox.Text = callsignAlreadyTyped;
        }
        finally
        {
            _restoringFromPriorSubmission = false;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CallsignBox.Text))
        {
            MessageBox.Show(this, "Callsign is required.", "Cabrillo Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(ContestBox.Text))
        {
            MessageBox.Show(this, "Contest name is required.", "Cabrillo Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new CabrilloContestInfo
        {
            Callsign = CallsignBox.Text.Trim().ToUpperInvariant(),
            Contest = ContestBox.Text.Trim().ToUpperInvariant(),
            CategoryOperator = CategoryOperatorBox.Text.Trim().ToUpperInvariant(),
            CategoryBand = CategoryBandBox.Text.Trim().ToUpperInvariant(),
            CategoryMode = CategoryModeBox.Text.Trim().ToUpperInvariant(),
            CategoryPower = CategoryPowerBox.Text.Trim().ToUpperInvariant(),
            CategoryStation = CategoryStationBox.Text.Trim().ToUpperInvariant(),
            Name = NameBox.Text.Trim(),
            Location = LocationBox.Text.Trim(),
            ClaimedScore = ClaimedScoreBox.Text.Trim(),
            Email = EmailBox.Text.Trim(),
            SoapBox = SoapBoxBox.Text.Trim(),
        };

        DialogResult = true;
        Close();
    }
}
