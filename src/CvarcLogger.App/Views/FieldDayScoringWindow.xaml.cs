using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class FieldDayScoringWindow : Window
{
    /// <summary>Standard ARRL Field Day bonus items with their fixed point values. Item ordering here
    /// mirrors the order they appear on the official ARRL FD Summary Sheet, so someone submitting a
    /// score by hand can tick them off in the same order. 100% Emergency Power and the GOTA Bonus are
    /// handled separately (see BuildCountedBonusRow) since both scale with a count rather than being a
    /// single fixed value: rule 7.3.1 pays 100 points per transmitter (max 20, 2,000-point cap), and
    /// rule 7.3.13.1 pays 5 points per GOTA-station contact with no cap.</summary>
    private static readonly (string Name, int Points)[] BonusCatalog =
    {
        ("Media Publicity", 100),
        ("Public Location", 100),
        ("Public Information Table", 100),
        ("Message to Section Manager / SEC", 100),
        ("Message Handling (10 pts each, max 10 messages)", 100),
        ("Satellite QSO", 100),
        ("Alternate Power (solar, methanol, etc.)", 100),
        ("W1AW Bulletin copied", 100),
        ("Educational Activity", 100),
        ("Site visit by served agency / elected official", 100),
        ("Social Media", 100),
        ("Web submission of the entry", 50),
        ("Youth Participation (20 pts per youth op, max 5)", 100),
        ("Safety Officer", 100),
        ("GOTA Coach (supervised >=10 GOTA contacts, rule 7.3.13.2)", 100),
    };

    private const int EmergencyPowerPointsPerTransmitter = 100;
    private const int EmergencyPowerMaxTransmitters = 20;
    private const int GotaPointsPerContact = 5;

    private readonly List<CheckBox> _bonusCheckboxes = new();
    private CountedBonusRow _emergencyPower = null!;
    private CountedBonusRow _gotaBonus = null!;

    public FieldDayScoringWindow()
    {
        InitializeComponent();
        YearBox.Text = DateTime.UtcNow.Year.ToString();
        BuildBonusList();
    }

    private void BuildBonusList()
    {
        BonusList.Children.Clear();
        _bonusCheckboxes.Clear();

        _emergencyPower = BuildCountedBonusRow(
            "100% Emergency Power", "transmitters", EmergencyPowerPointsPerTransmitter, EmergencyPowerMaxTransmitters);
        BonusList.Children.Add(_emergencyPower.Panel);

        _gotaBonus = BuildCountedBonusRow(
            "GOTA Bonus", "GOTA contacts", GotaPointsPerContact, maxCount: null);
        BonusList.Children.Add(_gotaBonus.Panel);

        foreach (var (name, points) in BonusCatalog)
        {
            var cb = new CheckBox
            {
                Content = $"{name}  ({points} pts)",
                Tag = new BonusEntry(name, points),
                Margin = new Thickness(0, 4, 0, 4),
                FontSize = 12,
            };
            cb.Checked += Bonus_CheckedChanged;
            cb.Unchecked += Bonus_CheckedChanged;
            _bonusCheckboxes.Add(cb);
            BonusList.Children.Add(cb);
        }

        UpdateBonusTotal();
    }

    private sealed class CountedBonusRow
    {
        public required StackPanel Panel { get; init; }
        public required CheckBox Checkbox { get; init; }
        public required TextBox CountBox { get; init; }
        public required TextBlock PointsLabel { get; init; }
        public required int PointsPerUnit { get; init; }
        public required int? MaxCount { get; init; }

        public int Count()
        {
            if (!int.TryParse(CountBox.Text, out int count) || count < 1)
                count = 1;
            return MaxCount is int max ? Math.Min(count, max) : count;
        }

        public int Points() => Checkbox.IsChecked == true ? Count() * PointsPerUnit : 0;
    }

    /// <summary>Builds a checkbox + numeric count field for a bonus that scales per-unit (transmitters,
    /// contacts, etc.) instead of being a single fixed value, e.g. "100% Emergency Power x 3 transmitters"
    /// or "GOTA Bonus x 12 contacts". Pass maxCount: null for an uncapped count (the GOTA bonus has no
    /// upper limit, unlike Emergency Power's 20-transmitter cap).</summary>
    private CountedBonusRow BuildCountedBonusRow(string checkboxLabel, string unitLabel, int pointsPerUnit, int? maxCount)
    {
        var container = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var checkbox = new CheckBox
        {
            Content = checkboxLabel,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
        };
        row.Children.Add(checkbox);

        row.Children.Add(new TextBlock { Text = "  ×", VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });

        var countBox = new TextBox
        {
            Text = "1",
            Width = maxCount is int m && m < 100 ? 32 : 44,
            Margin = new Thickness(4, 0, 4, 0),
            TextAlignment = TextAlignment.Center,
            FontSize = 12,
            MaxLength = maxCount is int mx ? mx.ToString().Length : 4,
            // Bypasses App.xaml's global TextBox style/ControlTemplate, which renders typed text
            // invisible/garbled for code-behind-constructed TextBoxes (same issue and same fix as the
            // Help window's search box). Plain default WPF rendering here is reliable.
            Style = null,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
        };
        // Digits only -- without this, non-numeric or overlong input left the field showing garbage
        // that int.TryParse silently fell back to 1 for, with no visible feedback that the typed value
        // wasn't being used.
        countBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        DataObject.AddPastingHandler(countBox, (s, e) =>
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text) || !((string)e.DataObject.GetData(DataFormats.Text)).All(char.IsDigit))
                e.CancelCommand();
        });
        countBox.GotFocus += (_, _) => countBox.SelectAll();
        row.Children.Add(countBox);

        string maxSuffix = maxCount is int cap ? $" (max {cap})" : "";
        row.Children.Add(new TextBlock { Text = $"{unitLabel}{maxSuffix}", VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        container.Children.Add(row);

        var pointsLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)new BrushConverter().ConvertFromString("#666666")!,
            Margin = new Thickness(20, 2, 0, 0),
        };
        container.Children.Add(pointsLabel);

        var result = new CountedBonusRow
        {
            Panel = container,
            Checkbox = checkbox,
            CountBox = countBox,
            PointsLabel = pointsLabel,
            PointsPerUnit = pointsPerUnit,
            MaxCount = maxCount,
        };

        void RefreshLabel() => result.PointsLabel.Text = $"{result.Count()} × {pointsPerUnit} pts = {result.Points()} pts";

        // Split label-only refresh from the full event handler: the full handler also calls
        // UpdateBonusTotal(), which reads both _emergencyPower and _gotaBonus -- but during BuildBonusList,
        // this row's own field (e.g. _emergencyPower) hasn't been assigned yet while the *other* row is
        // still being constructed, so calling UpdateBonusTotal() from here during initial setup would
        // null-ref. BuildBonusList already calls UpdateBonusTotal() itself once both rows exist.
        void Refresh(object? s, RoutedEventArgs e)
        {
            RefreshLabel();
            UpdateBonusTotal();
        }

        checkbox.Checked += Refresh;
        checkbox.Unchecked += Refresh;
        countBox.TextChanged += Refresh;
        RefreshLabel();

        return result;
    }

    private void Bonus_CheckedChanged(object sender, RoutedEventArgs e) => UpdateBonusTotal();

    private int UpdateBonusTotal()
    {
        int total = _emergencyPower.Points() + _gotaBonus.Points();
        foreach (var cb in _bonusCheckboxes)
        {
            if (cb.IsChecked == true && cb.Tag is BonusEntry entry) total += entry.Points;
        }
        BonusTotalLabel.Text = total.ToString();
        return total;
    }

    private IReadOnlyList<FieldDayBonusItem> GetSelectedBonuses()
    {
        var picked = new List<FieldDayBonusItem>();

        if (_emergencyPower.Checkbox.IsChecked == true)
        {
            int count = _emergencyPower.Count();
            picked.Add(new FieldDayBonusItem($"100% Emergency Power ({count} transmitter{(count == 1 ? "" : "s")} × {EmergencyPowerPointsPerTransmitter} pts)", count * EmergencyPowerPointsPerTransmitter));
        }

        if (_gotaBonus.Checkbox.IsChecked == true)
        {
            int count = _gotaBonus.Count();
            picked.Add(new FieldDayBonusItem($"GOTA Bonus ({count} contact{(count == 1 ? "" : "s")} × {GotaPointsPerContact} pts)", count * GotaPointsPerContact));
        }

        foreach (var cb in _bonusCheckboxes)
        {
            if (cb.IsChecked == true && cb.Tag is BonusEntry entry)
                picked.Add(new FieldDayBonusItem(entry.Name, entry.Points));
        }
        return picked;
    }

    private async void Recalculate_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(YearBox.Text, out int year) || year < 1950 || year > 2100)
        {
            MessageBox.Show(this, "Enter a valid year (e.g. 2026).", "Field Day",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var powerTag = (PowerClassBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "LowPower";
        if (!Enum.TryParse<FieldDayPowerClass>(powerTag, out var powerClass))
            powerClass = FieldDayPowerClass.LowPower;

        var selectedBonuses = GetSelectedBonuses();
        string? requiredContestId = StrictContestIdBox.IsChecked == true ? "ARRL-FIELD-DAY" : null;

        try
        {
            var qsoRepo = App.Services.GetRequiredService<IQsoRepository>();
            var allQsos = await qsoRepo.GetAllAsync();
            var fdQsos = FieldDayQsoFilter.ForYear(allQsos, year, requiredContestId).ToList();

            var breakdown = FieldDayScorer.Score(fdQsos, powerClass, selectedBonuses);
            RenderBreakdown(breakdown, year);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Scoring failed: {ex.Message}", "Field Day",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenderBreakdown(FieldDayScoreBreakdown b, int year)
    {
        ResultsPanel.Children.Clear();

        var (startUtc, endUtc) = FieldDayQsoFilter.WindowFor(year);
        Add(ResultsPanel, $"Field Day {year}", 16, FontWeights.Bold);
        Add(ResultsPanel, $"Window: {startUtc:yyyy-MM-dd HHmm}Z – {endUtc:yyyy-MM-dd HHmm}Z (fourth full weekend of June)",
            11, FontWeights.Normal, "#666666", topMargin: 2);
        AddDivider(ResultsPanel);

        Add(ResultsPanel, "QSO Breakdown", 13, FontWeights.Bold, topMargin: 6);
        AddRow(ResultsPanel, "Phone QSOs:",    $"{b.PhoneQsos,6}   × 1 = {b.PhonePoints,6} pts");
        AddRow(ResultsPanel, "CW QSOs:",       $"{b.CwQsos,6}   × 2 = {b.CwPoints,6} pts");
        AddRow(ResultsPanel, "Digital QSOs:",  $"{b.DigitalQsos,6}   × 2 = {b.DigitalPoints,6} pts");
        AddRow(ResultsPanel, "Total QSOs:",    $"{b.TotalQsos,6}");
        AddDivider(ResultsPanel);

        Add(ResultsPanel, "Score Calculation", 13, FontWeights.Bold, topMargin: 6);
        AddRow(ResultsPanel, "Raw QSO points:",         $"{b.RawQsoPoints,8}");
        AddRow(ResultsPanel, "Power multiplier:",       $"      × {b.PowerMultiplier}");
        AddRow(ResultsPanel, "Multiplied QSO points:",  $"{b.MultipliedQsoPoints,8}");
        AddDivider(ResultsPanel);

        Add(ResultsPanel, "Bonuses Claimed", 13, FontWeights.Bold, topMargin: 6);
        if (b.Bonuses.Count == 0)
        {
            Add(ResultsPanel, "(none selected)", 12, FontWeights.Normal, "#999999", topMargin: 4);
        }
        else
        {
            foreach (var item in b.Bonuses)
            {
                AddRow(ResultsPanel, item.Name + ":", $"+ {item.Points,4} pts");
            }
        }
        AddRow(ResultsPanel, "Bonus subtotal:", $"+ {b.BonusPoints,4} pts", isBold: true);
        AddDivider(ResultsPanel);

        AddRow(ResultsPanel, "FINAL SCORE:", $"{b.FinalScore,8}", isBold: true);

        AddDivider(ResultsPanel);
        Add(ResultsPanel, $"Sections/States Worked ({b.SectionsWorked.Count})", 13, FontWeights.Bold, topMargin: 6);
        if (b.SectionsWorked.Count == 0)
        {
            Add(ResultsPanel, "(none recorded on any QSO)", 12, FontWeights.Normal, "#999999", topMargin: 4);
        }
        else
        {
            Add(ResultsPanel, string.Join("  ", b.SectionsWorked), 12, FontWeights.Normal, "#333333", topMargin: 4);
        }
    }

    private static void Add(Panel host, string text, double size, FontWeight weight, string color = "#000000", double topMargin = 0)
    {
        host.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = (Brush)new BrushConverter().ConvertFromString(color)!,
            Margin = new Thickness(0, topMargin, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private static void AddRow(Panel host, string label, string value, bool isBold = false)
    {
        // Three-column layout: label wraps in its own share, value hugs a fixed narrow column right
        // next to it, and the third star column soaks up whatever space is left so the value never
        // pushes into (or gets clipped by) the card's right border. Keeps points and labels visually
        // adjacent instead of the value floating way off to the right.
        var grid = new Grid { Margin = new Thickness(0, 2, 24, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var v = new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        Grid.SetColumn(l, 0);
        Grid.SetColumn(v, 1);
        grid.Children.Add(l);
        grid.Children.Add(v);
        host.Children.Add(grid);
    }

    private static void AddDivider(Panel host)
    {
        host.Children.Add(new Border
        {
            Height = 1,
            Background = Brushes.LightGray,
            Margin = new Thickness(0, 8, 0, 8),
        });
    }

    private async void VerifyNow_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(YearBox.Text, out int year) || year < 1950 || year > 2100)
        {
            MessageBox.Show(this, "Enter a valid year (e.g. 2026).", "Field Day",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string? requiredContestId = StrictContestIdBox.IsChecked == true ? "ARRL-FIELD-DAY" : null;

        try
        {
            var qsoRepo = App.Services.GetRequiredService<IQsoRepository>();
            var allQsos = await qsoRepo.GetAllAsync();
            var verificationResults = FieldDayScorer.Verify(allQsos, year, requiredContestId);

            VerificationGrid.ItemsSource = verificationResults;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Verification failed: {ex.Message}", "Field Day",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private record BonusEntry(string Name, int Points);
}
