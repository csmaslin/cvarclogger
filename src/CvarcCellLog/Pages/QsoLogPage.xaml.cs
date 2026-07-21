using CvarcCellLog.Converters;
using CvarcCellLog.Models;
using CvarcCellLog.Services;
using CvarcCellLog.ViewModels;

namespace CvarcCellLog.Pages;

/// <summary>QSO Log: a single-line-per-record table with a fixed (non-scrolling) header row and
/// user-configurable columns (see LogColumnsPage). Column count/order is only known at runtime (it's
/// whatever the operator last saved via the Columns screen), so both the header row and the
/// CollectionView's row template are built in code here rather than declared statically in XAML --
/// see BuildHeader/BuildRowTemplate. Both are rebuilt from the exact same column list every time, so
/// the header and every row always line up.</summary>
public partial class QsoLogPage : ContentPage
{
    private readonly QsoLogViewModel _viewModel;
    private readonly QsoCellValueConverter _cellValueConverter = new();
    private List<LogColumnDefinition> _currentColumns = new();

    public QsoLogPage(QsoLogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RebuildColumns();
        await _viewModel.RefreshAsync();
    }

    private void RebuildColumns()
    {
        _currentColumns = LogColumnPreferences.Load().Select(LogColumns.Get).ToList();
        BuildHeader(_currentColumns);
        QsoCollectionView.ItemTemplate = BuildRowTemplate(_currentColumns);
    }

    /// <summary>Builds the fixed header row, one tappable Label per column -- tapping a header sorts
    /// the log by that column (toggling direction on a second tap of the same one), mirroring a
    /// desktop spreadsheet. An arrow suffix on the currently-sorted column's label is the only visual
    /// sort indicator, so this gets rebuilt (cheap -- just a handful of Labels) after every sort tap to
    /// keep that arrow in sync; see OnHeaderColumnTapped.</summary>
    private void BuildHeader(IReadOnlyList<LogColumnDefinition> columns)
    {
        HeaderGrid.ColumnDefinitions.Clear();
        HeaderGrid.Children.Clear();

        for (int i = 0; i < columns.Count; i++)
        {
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(columns[i].Weight, GridUnitType.Star)));

            string headerText = columns[i].Header;
            if (_viewModel.SortColumn == columns[i].Key)
                headerText += _viewModel.SortAscending ? " ▲" : " ▼";

            var label = new Label
            {
                Text = headerText,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                LineBreakMode = LineBreakMode.TailTruncation,
            };

            var key = columns[i].Key;
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (_, _) => OnHeaderColumnTapped(key);
            label.GestureRecognizers.Add(tapGesture);

            Grid.SetColumn(label, i);
            HeaderGrid.Children.Add(label);
        }
    }

    private void OnHeaderColumnTapped(LogColumnKey key)
    {
        _viewModel.SetSort(key);
        BuildHeader(_currentColumns);
    }

    private DataTemplate BuildRowTemplate(IReadOnlyList<LogColumnDefinition> columns)
    {
        return new DataTemplate(() =>
        {
            var grid = new Grid { Padding = new Thickness(6, 10), ColumnSpacing = 6 };
            for (int i = 0; i < columns.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(columns[i].Weight, GridUnitType.Star)));

                var label = new Label { FontSize = 13, LineBreakMode = LineBreakMode.TailTruncation };
                label.SetBinding(Label.TextProperty, new Binding(".", converter: _cellValueConverter, converterParameter: columns[i].Key));
                Grid.SetColumn(label, i);
                grid.Children.Add(label);
            }

            var tapGesture = new TapGestureRecognizer { Command = _viewModel.EditCommand };
            tapGesture.SetBinding(TapGestureRecognizer.CommandParameterProperty, new Binding("."));
            grid.GestureRecognizers.Add(tapGesture);

            var deleteItem = new SwipeItem { Text = "Delete", BackgroundColor = Color.FromArgb("#C0392B"), Command = _viewModel.DeleteCommand };
            deleteItem.SetBinding(SwipeItem.CommandParameterProperty, new Binding("."));

            return new SwipeView
            {
                RightItems = new SwipeItems { deleteItem },
                Content = grid,
            };
        });
    }

    private async void OnNewQsoClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(QsoEntryPage));
    }

    private async void OnColumnsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LogColumnsPage));
    }
}
