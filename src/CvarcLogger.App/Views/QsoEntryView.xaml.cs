using System.Windows;
using System.Windows.Controls;
using CvarcLogger.App.ViewModels;

namespace CvarcLogger.App.Views;

public partial class QsoEntryView : UserControl
{
    public QsoEntryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is QsoEntryViewModel oldViewModel)
            oldViewModel.FieldVisibilityChanged -= OnFieldVisibilityChanged;

        if (e.NewValue is QsoEntryViewModel newViewModel)
            newViewModel.FieldVisibilityChanged += OnFieldVisibilityChanged;

        ApplyFieldVisibility();
    }

    private void OnFieldVisibilityChanged(object? sender, EventArgs e) => ApplyFieldVisibility();

    /// <summary>Hides/shows the entry form's optional fields in step with the log grid's Choose
    /// Columns picker. Callsign, Station, Date/Time (UTC), Local Time, Band, Mode, and Sub-Mode are
    /// deliberately never touched here -- Band/Mode are required to log any QSO at all, and the rest
    /// either have no matching column (Station) or already manage their own visibility (Sub-Mode, via
    /// SubModeVisibilityConverter).</summary>
    private void ApplyFieldVisibility()
    {
        if (DataContext is not QsoEntryViewModel viewModel) return;

        TimeOffPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("TimeOff"));
        FreqPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Freq"));
        RstSentPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Rst"));
        RstRcvdPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Rst"));
        NamePanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Name"));
        GridPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Grid"));
        CityPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("City"));
        StatePanel.Visibility = ToVisibility(viewModel.IsFieldVisible("State"));
        CountyPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("County"));
        CountryPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Country"));
        ArrlSectionPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("ArrlSection"));
        CqZonePanel.Visibility = ToVisibility(viewModel.IsFieldVisible("CqZone"));
        ItuZonePanel.Visibility = ToVisibility(viewModel.IsFieldVisible("ItuZone"));
        MySotaPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("MySota"));
        SotaPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Sota"));
        MyPotaPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("MyPota"));
        PotaPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Pota"));
        OpPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Op"));
        QthPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Qth"));
        TxPowerPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("TxPower"));
        CommentPanel.Visibility = ToVisibility(viewModel.IsFieldVisible("Comment"));
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
