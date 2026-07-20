using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CvarcLogger.App.ViewModels;
using CvarcLogger.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class QsoLogGridView : UserControl
{
    /// <summary>The row last touched by a plain click or a Ctrl/Shift-right-click, used as the start
    /// point for the next Shift-right-click range selection.</summary>
    private Qso? _rangeAnchorQso;

    /// <summary>Guards against re-attaching the column-resize-thumb listeners more than once, in case
    /// DataContextChanged ever fires again after the first real DataContext is set.</summary>
    private bool _hasHookedColumnResizeThumbs;

    public QsoLogGridView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is QsoLogViewModel oldViewModel)
            oldViewModel.ColumnVisibilityChanged -= OnColumnVisibilityChanged;

        if (e.NewValue is QsoLogViewModel newViewModel)
            newViewModel.ColumnVisibilityChanged += OnColumnVisibilityChanged;

        ApplyColumnVisibility();
        ApplyColumnOrder();
        ApplyColumnWidths();
    }

    /// <summary>Restores each column's saved pixel width. Columns without a saved width (never resized,
    /// or added in a later app version) keep their XAML-declared default.</summary>
    private void ApplyColumnWidths()
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        var widths = viewModel.ColumnWidths;
        if (widths.Count == 0) return;

        foreach (var column in LogDataGrid.Columns)
        {
            if (ColumnKey.GetKey(column) is string key && widths.TryGetValue(key, out var width))
                column.Width = new DataGridLength(width);
        }
    }

    /// <summary>Detects a genuine user-driven column resize via the column headers' own resize-gripper
    /// Thumb controls (named "PART_LeftHeaderGripper"/"PART_RightHeaderGripper" in DataGridColumnHeader's
    /// default template) rather than watching DataGridColumn.Width itself. Width also changes for
    /// reasons that have nothing to do with the user dragging a border -- WPF resolves/squeezes
    /// pixel-width columns to fit the viewport as row data loads in and headers finish rendering, which
    /// fires the same change notification and was observed clobbering freshly-restored saved widths with
    /// squeezed ones (tried gating on both the initial ApplyColumnWidths call and the Loaded event;
    /// neither was late enough). Thumb.DragCompleted only fires for an actual mouse drag, sidestepping
    /// the timing question entirely.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hasHookedColumnResizeThumbs) return;
        _hasHookedColumnResizeThumbs = true;
        Dispatcher.BeginInvoke(new Action(HookColumnResizeThumbs), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void HookColumnResizeThumbs()
    {
        foreach (var header in FindVisualChildren<System.Windows.Controls.Primitives.DataGridColumnHeader>(LogDataGrid))
        {
            foreach (var thumb in FindVisualChildren<System.Windows.Controls.Primitives.Thumb>(header))
            {
                if (thumb.Name is "PART_LeftHeaderGripper" or "PART_RightHeaderGripper")
                    thumb.DragCompleted += OnColumnResizeDragCompleted;
            }
        }
    }

    private void OnColumnResizeDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        var widths = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in LogDataGrid.Columns)
        {
            if (ColumnKey.GetKey(column) is string key && column.Width.IsAbsolute)
                widths[key] = column.Width.DisplayValue;
        }

        viewModel.SaveColumnWidths(widths);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) yield return typedChild;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    /// <summary>Restores the saved left-to-right column order. Columns without a saved position (never
    /// reordered, or added since) keep their XAML-declared relative order and land after every column
    /// that does have one.</summary>
    private void ApplyColumnOrder()
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        var order = viewModel.ColumnOrder;
        if (order.Count == 0) return;

        var columns = LogDataGrid.Columns
            .OrderBy(c => ColumnKey.GetKey(c) is string key && order.TryGetValue(key, out var index) ? index : int.MaxValue)
            .ThenBy(c => c.DisplayIndex)
            .ToList();

        for (int i = 0; i < columns.Count; i++)
            columns[i].DisplayIndex = i;
    }

    private void LogDataGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        var keysInDisplayOrder = LogDataGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(ColumnKey.GetKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToList();

        viewModel.SaveColumnOrder(keysInDisplayOrder);
    }

    private void OnColumnVisibilityChanged(object? sender, EventArgs e) => ApplyColumnVisibility();

    private void ApplyColumnVisibility()
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        UtcTimeColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("UtcTime"));
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
        QthColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Qth"));
        OpColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Op"));
        MySotaColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("MySota"));
        SotaColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Sota"));
        MyPotaColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("MyPota"));
        PotaColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Pota"));
        CommentColumn.Visibility = ToVisibility(viewModel.IsColumnVisible("Comment"));
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void LogDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.Header = DataContext is QsoLogViewModel viewModel && e.Row.Item is Qso qso
            ? viewModel.GetLogNumber(qso).ToString()
            : (e.Row.GetIndex() + 1).ToString();
    }

    private void LogDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not QsoLogViewModel viewModel) return;

        viewModel.SelectedQsos.Clear();
        foreach (var item in LogDataGrid.SelectedItems)
        {
            if (item is Qso qso) viewModel.SelectedQsos.Add(qso);
        }

        if (LogDataGrid.SelectedItem is Qso current) _rangeAnchorQso = current;
    }

    /// <summary>Ctrl-right-click toggles a single row into/out of the multi-selection; Shift-right-click
    /// selects every row between the last-touched row (the "anchor") and the clicked one, in the grid's
    /// current sorted/filtered display order. Plain right-click is left untouched.</summary>
    private void LogDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not Qso qso) return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (LogDataGrid.SelectedItems.Contains(qso)) LogDataGrid.SelectedItems.Remove(qso);
            else LogDataGrid.SelectedItems.Add(qso);
            _rangeAnchorQso = qso;
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Shift && _rangeAnchorQso is not null)
        {
            SelectRange(_rangeAnchorQso, qso);
            e.Handled = true;
        }
    }

    private void SelectRange(Qso anchor, Qso target)
    {
        var items = LogDataGrid.Items;
        int anchorIndex = items.IndexOf(anchor);
        int targetIndex = items.IndexOf(target);
        if (anchorIndex < 0 || targetIndex < 0) return;

        int start = Math.Min(anchorIndex, targetIndex);
        int end = Math.Max(anchorIndex, targetIndex);

        LogDataGrid.SelectedItems.Clear();
        for (int i = start; i <= end; i++) LogDataGrid.SelectedItems.Add(items[i]);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private async void LogDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;
        await PasteFromClipboardAsync();
    }

    /// <summary>Reads tab-separated rows from the clipboard and logs each as a new QSO. Cells are
    /// interpreted using the grid's *currently visible* columns in their current left-to-right order --
    /// exactly what Ctrl+C on this same DataGrid produces (WPF's built-in copy already skips hidden
    /// columns and joins visible ones with tabs), so a copy/paste round-trip lines fields up correctly
    /// as long as the column layout hasn't changed in between.</summary>
    private async Task PasteFromClipboardAsync()
    {
        if (DataContext is not QsoLogViewModel viewModel) return;
        if (!Clipboard.ContainsText()) return;

        string[] lines = Clipboard.GetText()
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => line.Length > 0)
            .ToArray();
        if (lines.Length == 0) return;

        var columnKeysInOrder = LogDataGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .OrderBy(c => c.DisplayIndex)
            .Select(ColumnKey.GetKey)
            .ToList();

        var qsos = new List<Qso>();
        foreach (var line in lines)
        {
            string[] cells = line.Split('\t');
            var qso = new Qso();
            for (int i = 0; i < cells.Length && i < columnKeysInOrder.Count; i++)
            {
                string? key = columnKeysInOrder[i];
                string cellText = cells[i];
                if (key is null || string.IsNullOrEmpty(cellText)) continue;
                if (PasteFieldSetters.TryGetValue(key, out var setter)) setter(qso, cellText);
            }

            if (string.IsNullOrWhiteSpace(qso.Callsign)) continue;
            if (qso.QsoDateTimeOnUtc == default) qso.QsoDateTimeOnUtc = DateTime.UtcNow;
            qsos.Add(qso);
        }

        if (qsos.Count == 0) return;
        await viewModel.AddPastedQsosAsync(qsos);
    }

    /// <summary>Maps a column key (see local:ColumnKey) to the Qso field it round-trips through on
    /// paste. "LocalTime" has no setter (it's computed from QsoDateTimeOnUtc + station UTC offset) so
    /// it's intentionally absent -- that cell is just ignored if pasted.</summary>
    private static readonly Dictionary<string, Action<Qso, string>> PasteFieldSetters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UtcTime"] = (qso, text) => { var dt = ParsePastedUtcDateTime(text); if (dt.HasValue) qso.QsoDateTimeOnUtc = dt.Value; },
        ["Callsign"] = (qso, text) => qso.Callsign = text.Trim().ToUpperInvariant(),
        ["Band"] = (qso, text) => qso.Band = text,
        ["Mode"] = (qso, text) => qso.Mode = text,
        ["Freq"] = (qso, text) => qso.FrequencyMhz = ParsePastedDecimal(text),
        ["Rst"] = (qso, text) => { var (sent, rcvd) = SplitPastedPair(text); qso.RstSent = sent; qso.RstRcvd = rcvd; },
        ["Name"] = (qso, text) => qso.Name = text,
        ["Grid"] = (qso, text) => qso.GridSquare = text,
        ["City"] = (qso, text) => qso.City = text,
        ["State"] = (qso, text) => qso.State = text.Trim().ToUpperInvariant(),
        ["County"] = (qso, text) => qso.County = text,
        ["Country"] = (qso, text) => qso.Country = text,
        ["ArrlSection"] = (qso, text) => qso.ArrlSection = text.Trim().ToUpperInvariant(),
        ["CqZone"] = (qso, text) => qso.CqZone = ParsePastedInt(text),
        ["ItuZone"] = (qso, text) => qso.ItuZone = ParsePastedInt(text),
        ["Continent"] = (qso, text) => qso.Continent = text,
        ["SubMode"] = (qso, text) => qso.SubMode = text,
        ["FreqRx"] = (qso, text) => qso.FrequencyRxMhz = ParsePastedDecimal(text),
        ["TxPower"] = (qso, text) => qso.TxPowerWatts = ParsePastedDecimal(text),
        ["Qsl"] = (qso, text) => { var (sent, rcvd) = SplitPastedPair(text); qso.QslSent = ParsePastedQslStatus(sent); qso.QslRcvd = ParsePastedQslStatus(rcvd); },
        ["Lotw"] = (qso, text) => { var (sent, rcvd) = SplitPastedPair(text); qso.LotwQslSent = ParsePastedQslStatus(sent); qso.LotwQslRcvd = ParsePastedQslStatus(rcvd); },
        ["QslVia"] = (qso, text) => qso.QslViaCallsign = text,
        ["TimeOff"] = (qso, text) => qso.QsoDateTimeOffUtc = ParsePastedUtcDateTime(text),
        ["Station"] = (qso, text) => qso.StationCallsign = text.Trim().ToUpperInvariant(),
        ["Operator"] = (qso, text) => qso.OperatorCallsign = text,
        ["MyGrid"] = (qso, text) => qso.MyGridSquare = text,
        ["MyState"] = (qso, text) => qso.MyState = text,
        ["MyCounty"] = (qso, text) => qso.MyCounty = text,
        ["Qth"] = (qso, text) => qso.Qth = text,
        ["Op"] = (qso, text) => qso.Op = text,
        ["MySota"] = (qso, text) => qso.MySotaRef = text,
        ["Sota"] = (qso, text) => qso.SotaRef = text,
        ["MyPota"] = (qso, text) => qso.MySigInfo = text,
        ["Pota"] = (qso, text) => qso.SigInfo = text,
        ["Comment"] = (qso, text) => qso.Comment = text,
    };

    private static DateTime? ParsePastedUtcDateTime(string text) =>
        DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : null;

    private static decimal? ParsePastedDecimal(string text) =>
        decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static int? ParsePastedInt(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    private static (string? First, string? Second) SplitPastedPair(string text)
    {
        var parts = text.Split('/', 2);
        string? first = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0].Trim() : null;
        string? second = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : null;
        return (first, second);
    }

    private static QslStatus ParsePastedQslStatus(string? text) =>
        !string.IsNullOrWhiteSpace(text) && Enum.TryParse<QslStatus>(text.Trim(), true, out var status) ? status : QslStatus.NotSent;

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
