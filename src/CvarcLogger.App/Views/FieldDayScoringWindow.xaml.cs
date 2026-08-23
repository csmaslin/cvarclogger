using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class FieldDayScoringWindow : Window
{
    public FieldDayScoringWindow()
    {
        InitializeComponent();
        YearBox.Text = DateTime.UtcNow.Year.ToString();
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

        int.TryParse(BonusBox.Text, out int bonus);

        string? requiredContestId = StrictContestIdBox.IsChecked == true ? "ARRL-FIELD-DAY" : null;

        try
        {
            var qsoRepo = App.Services.GetRequiredService<IQsoRepository>();
            var allQsos = await qsoRepo.GetAllAsync();
            var fdQsos = FieldDayQsoFilter.ForYear(allQsos, year, requiredContestId).ToList();

            var breakdown = FieldDayScorer.Score(fdQsos, powerClass, bonus);
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
        Add(ResultsPanel, $"Window: {startUtc:yyyy-MM-dd HHmm}Z – {endUtc:yyyy-MM-dd HHmm}Z", 11, FontWeights.Normal, "#666666", topMargin: 2);
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
        AddRow(ResultsPanel, "Bonus points:",           $"      + {b.BonusPoints}");
        AddDivider(ResultsPanel);
        AddRow(ResultsPanel, "FINAL SCORE:",            $"{b.FinalScore,8}", isBold: true);

        AddDivider(ResultsPanel);
        Add(ResultsPanel, $"Sections/States Worked ({b.SectionsWorked.Count})", 13, FontWeights.Bold, topMargin: 6);
        if (b.SectionsWorked.Count == 0)
        {
            Add(ResultsPanel, "(none recorded on any QSO)", 12, FontWeights.Normal, "#999999", topMargin: 4);
        }
        else
        {
            // Wrap sections in a horizontal list -- long enough at 84 sections that a comma-join keeps
            // the UI compact instead of scrolling one-per-line.
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
        });
    }

    private static void AddRow(Panel host, string label, string value, bool isBold = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = new TextBlock { Text = label, FontFamily = new FontFamily("Consolas"), FontSize = 13, FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal };
        var v = new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), FontSize = 13, FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal };
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
}
