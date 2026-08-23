using System.Windows;
using CvarcLogger.Core.Cabrillo;

namespace CvarcLogger.App.Views;

public partial class CabrilloExportDialog : Window
{
    public CabrilloContestInfo? Result { get; private set; }

    public CabrilloExportDialog(CabrilloContestInfo? defaults = null)
    {
        InitializeComponent();

        var info = defaults ?? new CabrilloContestInfo();
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
