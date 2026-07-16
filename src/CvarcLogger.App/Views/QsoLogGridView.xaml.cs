using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class QsoLogGridView : UserControl
{
    public QsoLogGridView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is QsoLogViewModel oldViewModel)
            oldViewModel.ColumnVisibilityChanged -= OnColumnVisibilityChanged;

        if (e.NewValue is QsoLogViewModel newViewModel)
            newViewModel.ColumnVisibilityChanged += OnColumnVisibilityChanged;

        ApplyColumnVisibility();
    }

    private void OnColumnVisibilityChanged(object? sender, EventArgs e) => ApplyColumnVisibility();

    private void ApplyColumnVisibility()
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        LocalTimeColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("LocalTime"));
        BandColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Band"));
        ModeColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Mode"));
        FreqColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Freq"));
        RstColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Rst"));
        NameColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Name"));
        GridColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Grid"));
        CityColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("City"));
        StateColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("State"));
        CountyColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("County"));
        CountryColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Country"));
        ArrlSectionColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("ArrlSection"));
        CqZoneColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("CqZone"));
        ItuZoneColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("ItuZone"));
        ContinentColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Continent"));
        SubModeColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("SubMode"));
        FreqRxColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("FreqRx"));
        TxPowerColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("TxPower"));
        QslColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Qsl"));
        LotwColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Lotw"));
        QslViaColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("QslVia"));
        TimeOffColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("TimeOff"));
        StationColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Station"));
        OperatorColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Operator"));
        MyGridColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("MyGrid"));
        MyStateColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("MyState"));
        MyCountyColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("MyCounty"));
        NotesColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Notes"));
        CommentColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Comment"));
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void ColumnsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        var window = new ColumnPickerWindow(viewModel) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e) => OpenEditor();

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenEditor();

    private void OpenEditor()
    {
        if (DataContext is not QsoLogViewModel viewModel || viewModel.SelectedQso is null) return;

        var window = App.Services.GetRequiredService<QsoEditWindow>();
        window.Owner = Window.GetWindow(this);
        window.LoadQso(viewModel.SelectedQso);
        if (window.ShowDialog() == true)
        {
            _ = viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }
}
