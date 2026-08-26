using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using CvarcLogger.Core.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class SkccScoringWindow : Window
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public SkccScoringWindow()
    {
        InitializeComponent();
    }

    private void EventTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The "Special Member" callsign input only applies to the Weekday Sprint (its "Designated
        // Special SKCC Member" varies sprint to sprint); the soapbox/photo checkbox only applies to the
        // QSO Party. Hide whichever doesn't apply to the selected event so it can't be mistakenly filled
        // in for an event it has no effect on.
        if (SpecialMemberPanel is null || SoapboxPhotoBox is null) return; // fires once during InitializeComponent, before these exist

        var tag = SelectedEventType();
        SpecialMemberPanel.Visibility = tag == SkccEventType.WeekdaySprint ? Visibility.Visible : Visibility.Collapsed;
        SoapboxPhotoBox.Visibility = tag == SkccEventType.QsoParty ? Visibility.Visible : Visibility.Collapsed;
    }

    private SkccEventType SelectedEventType()
    {
        var tag = (EventTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "WeekdaySprint";
        return Enum.TryParse<SkccEventType>(tag, out var eventType) ? eventType : SkccEventType.WeekdaySprint;
    }

    private async void Recalculate_Click(object sender, RoutedEventArgs e)
    {
        var eventType = SelectedEventType();

        DateTime? start = null, end = null;
        if (!string.IsNullOrWhiteSpace(StartUtcBox.Text))
        {
            if (!DateTime.TryParseExact(StartUtcBox.Text, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
            {
                MessageBox.Show(this, "Event start must be in the format yyyy-MM-dd HH:mm, or left blank.", "SKCC",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            start = DateTime.SpecifyKind(s, DateTimeKind.Utc);
        }
        if (!string.IsNullOrWhiteSpace(EndUtcBox.Text))
        {
            if (!DateTime.TryParseExact(EndUtcBox.Text, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var en))
            {
                MessageBox.Show(this, "Event end must be in the format yyyy-MM-dd HH:mm, or left blank.", "SKCC",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            end = DateTime.SpecifyKind(en, DateTimeKind.Utc);
        }

        try
        {
            var qsoRepo = App.Services.GetRequiredService<IQsoRepository>();
            var allQsos = await qsoRepo.GetAllAsync();
            var windowQsos = allQsos.Where(q =>
                (start is null || q.QsoDateTimeOnUtc >= start) &&
                (end is null || q.QsoDateTimeOnUtc <= end));

            var breakdown = SkccScorer.Score(
                windowQsos,
                eventType,
                specialMemberCallsign: SpecialMemberBox.Text,
                soapboxPhotoSubmitted: SoapboxPhotoBox.IsChecked == true);

            RenderBreakdown(breakdown, start, end);
            MultiplierGrid.ItemsSource = breakdown.MultiplierDetails;
            TierBonusGrid.ItemsSource = breakdown.TierBonusDetails;
            ExtraBonusGrid.ItemsSource = breakdown.ExtraBonuses;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Scoring failed: {ex.Message}", "SKCC",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenderBreakdown(SkccScoreBreakdown b, DateTime? start, DateTime? end)
    {
        ResultsPanel.Children.Clear();

        string window = start is null && end is null
            ? "All logged QSOs (no start/end set)"
            : $"{start?.ToString("yyyy-MM-dd HHmm", CultureInfo.InvariantCulture) ?? "(no start)"}Z – {end?.ToString("yyyy-MM-dd HHmm", CultureInfo.InvariantCulture) ?? "(no end)"}Z";

        Add(ResultsPanel, EventDisplayName(b.EventType), 16, FontWeights.Bold);
        Add(ResultsPanel, $"Window: {window}", 11, FontWeights.Normal, "#666666", topMargin: 2);
        AddDivider(ResultsPanel);

        Add(ResultsPanel, "QSO Points", 13, FontWeights.Bold, topMargin: 6);
        AddRow(ResultsPanel, "Total QSOs in window:", $"{b.TotalQsos,6}");
        AddRow(ResultsPanel, "QSO points (unique callsign+band):", $"{b.TotalQsoPoints,6}");
        AddDivider(ResultsPanel);

        Add(ResultsPanel, "Multipliers", 13, FontWeights.Bold, topMargin: 6);
        AddRow(ResultsPanel, "Unique SPCs worked:", $"{b.TotalMultipliers,6}");
        AddDivider(ResultsPanel);

        Add(ResultsPanel, "Score Calculation", 13, FontWeights.Bold, topMargin: 6);
        AddRow(ResultsPanel, "QSO points x Multipliers:", $"{b.TotalQsoPoints} x {b.TotalMultipliers} = {b.TotalQsoPoints * b.TotalMultipliers,6}");
        AddDivider(ResultsPanel);

        Add(ResultsPanel, "Bonus Points", 13, FontWeights.Bold, topMargin: 6);
        AddRow(ResultsPanel, "Member tier bonuses (C/T/S):", $"+ {b.TierBonusPoints,4} pts");
        AddRow(ResultsPanel, "Other bonuses:", $"+ {b.ExtraBonusPoints,4} pts");
        AddRow(ResultsPanel, "Bonus subtotal:", $"+ {b.BonusPoints,4} pts", isBold: true);
        AddDivider(ResultsPanel);

        AddRow(ResultsPanel, "FINAL SCORE:", $"{b.FinalScore,8}", isBold: true);

        if (b.TierBonusDetails.Count == 0 && b.ExtraBonuses.Count == 0)
        {
            AddDivider(ResultsPanel);
            Add(ResultsPanel, "(no member or extra bonuses -- see the Multiplier & Bonus Details tab)", 11, FontWeights.Normal, "#999999", topMargin: 4);
        }
    }

    private static string EventDisplayName(SkccEventType eventType) => eventType switch
    {
        SkccEventType.WeekdaySprint => "Weekday Sprint (SKS)",
        SkccEventType.WeekendSprintathon => "Weekend Sprintathon (WES)",
        SkccEventType.EuropeSprint => "Europe Sprint (SKSE)",
        SkccEventType.AsiaSprint => "Asia Sprint (SKSA)",
        SkccEventType.QsoParty => "QSO Party",
        _ => eventType.ToString(),
    };

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
        var grid = new Grid { Margin = new Thickness(0, 2, 24, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
