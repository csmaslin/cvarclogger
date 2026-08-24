using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CvarcLogger.App.Views;

public partial class SprintsScoringWindow : Window
{
    public SprintsScoringWindow()
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<SprintsViewModel>();
        DataContext = viewModel;
    }

    private async void ScoreCw_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseRange(CwDateBox.Text, CwStartBox.Text, CwEndBox.Text, out var start, out var end))
            return;

        await ((SprintsViewModel)DataContext).ScoreCwAsync(start, end);
    }

    private async void ScoreSsb_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseRange(SsbDateBox.Text, SsbStartBox.Text, SsbEndBox.Text, out var start, out var end))
            return;

        await ((SprintsViewModel)DataContext).ScoreSsbAsync(start, end);
    }

    private async void ScoreRtty_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseRange(RttyDateBox.Text, RttyStartBox.Text, RttyEndBox.Text, out var start, out var end))
            return;

        await ((SprintsViewModel)DataContext).ScoreRttyAsync(start, end);
    }

    private async void ScoreVhf_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseRange(VhfDateBox.Text, VhfStartBox.Text, VhfEndBox.Text, out var start, out var end))
            return;

        bool isJanuary = (VhfMonthCombo.SelectedItem as ComboBoxItem)?.Content as string == "January";
        await ((SprintsViewModel)DataContext).ScoreVhfAsync(start, end, isJanuary);
    }

    private bool TryParseRange(string dateText, string startTimeText, string endTimeText, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;

        if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            !TimeSpan.TryParse(startTimeText, CultureInfo.InvariantCulture, out var startTime) ||
            !TimeSpan.TryParse(endTimeText, CultureInfo.InvariantCulture, out var endTime))
        {
            MessageBox.Show(this, "Enter the date as yyyy-MM-dd and both times as HH:mm (UTC).", "Sprints",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        start = DateTime.SpecifyKind(date.Date + startTime, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(date.Date + endTime, DateTimeKind.Utc);

        // A Sprint whose end time-of-day is earlier than its start (e.g. 23:00-01:00) rolled past
        // midnight UTC into the next day.
        if (end <= start)
            end = end.AddDays(1);

        return true;
    }
}
