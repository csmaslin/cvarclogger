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
    /// score by hand can tick them off in the same order.</summary>
    private static readonly (string Name, int Points)[] BonusCatalog =
    {
        ("100% Emergency Power (100 pts per transmitter, max 20)", 100),
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
    };

    private readonly List<CheckBox> _bonusCheckboxes = new();

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

    private void Bonus_CheckedChanged(object sender, RoutedEventArgs e) => UpdateBonusTotal();

    private int UpdateBonusTotal()
    {
        int total = 0;
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
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var l = new TextBlock { Text = label, FontFamily = new FontFamily("Consolas"), FontSize = 13, FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        var v = new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), FontSize = 13, FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Right };
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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private record BonusEntry(string Name, int Points);
}
