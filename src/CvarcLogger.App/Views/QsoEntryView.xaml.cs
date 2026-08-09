using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class QsoEntryView : UserControl
{
    // Fallback row/position (1-based) for any field the operator hasn't dragged yet -- matches the
    // hand-tuned layout the GUI redesign settled on (5 fields max per row). The persisted-override
    // side is SettingsService.GetEntryFormFieldPositions / EntryFormFieldPosition.
    private static readonly Dictionary<string, (int Row, int Position)> DefaultPositions = new()
    {
        ["Station"] = (1, 1), ["Callsign"] = (1, 2), ["UtcTime"] = (1, 3), ["Band"] = (1, 4), ["Freq"] = (1, 5),
        ["Mode"] = (2, 1), ["SubMode"] = (2, 2), ["LocalTime"] = (2, 3),
        ["TimeOff"] = (3, 1), ["RstSent"] = (3, 2), ["RstRcvd"] = (3, 3), ["Name"] = (3, 4), ["Grid"] = (3, 5),
        ["City"] = (4, 1), ["State"] = (4, 2), ["County"] = (4, 3), ["Country"] = (4, 4), ["ArrlSection"] = (4, 5),
        ["CqZone"] = (5, 1), ["ItuZone"] = (5, 2), ["Comment"] = (5, 3), ["Op"] = (5, 4), ["TxPower"] = (5, 5),

        // Stage 5: fields with a ShowXxxField ViewModel property but no entry-form control until now
        // (see the drag-drop feature's memory notes). Placed on their own rows below the pre-existing
        // ones so they don't disturb the already-tuned Normal-mode layout; visible by default only for
        // "Qsl" (matches QsoLogViewModel's existing column default), hidden for the rest.
        ["Qth"] = (6, 1), ["FreqRx"] = (6, 2), ["Continent"] = (6, 3), ["MyGrid"] = (6, 4), ["MyState"] = (6, 5),
        ["MyCounty"] = (7, 1), ["QslSent"] = (7, 2), ["QslRcvd"] = (7, 3), ["LotwQslSent"] = (7, 4), ["LotwQslRcvd"] = (7, 5),

        // Second gap-audit round: same rationale as the row-6/7 block above, just a larger batch of
        // fields found the second time (SOTA/POTA/contest-exchange/SKCC/sequence/QSL-via). All 11 of
        // these default hidden, matching the grid's own column defaults.
        ["Skcc"] = (8, 1), ["MySkcc"] = (8, 2), ["Precedence"] = (8, 3), ["Check"] = (8, 4), ["Class"] = (8, 5),
        ["MySota"] = (9, 1), ["Sota"] = (9, 2), ["MyPota"] = (9, 3), ["Pota"] = (9, 4), ["Sequence"] = (9, 5),
        ["QslVia"] = (10, 1),
    };

    // Custom drag data format, distinct from the default string format -- a plain string format would
    // collide with WPF's own built-in "drag text out of a TextBox" gesture, which also carries a string
    // payload. Field drags never start from inside a TextBox/ComboBox anyway (see IsInputControl), but
    // keeping the format name distinct removes any ambiguity in the Drop handler regardless.
    private const string FieldDragFormat = "CvarcLogger.QsoEntryFieldKey";

    private QsoEntryViewModel? _subscribedViewModel;
    private Dictionary<FrameworkElement, string>? _keyByElement;
    private Point? _dragStartPoint;
    private string? _dragCandidateKey;

    // Every mode that has its own saved field-position map, independent of whether it's reachable from
    // the sidebar yet (Net isn't -- see QsoEntryModeOptions -- but still gets a settings slot).
    private static readonly string[] AllModeNames = { "Normal", "Contest", "Sota", "Pota", "Net", "All" };

    public QsoEntryView()
    {
        InitializeComponent();
        ResolveFieldPositionCollisionsForAllModes();
        DataContextChanged += QsoEntryView_DataContextChanged;
    }

    // One-time startup self-heal, across every mode regardless of which one is actually being viewed.
    // Before FieldsGrid_Drop's collision check existed (see there), a drop onto empty-looking space could
    // silently land on a cell a currently-hidden field (Visibility=Collapsed, but still occupying its
    // Grid.Row/Column -- see ApplyFieldLayout) already used, saving two fields onto the same slot. New
    // drops can't create that anymore, but a position saved before that fix landed needed a real, one-time
    // repair rather than just hiding the symptom on screen.
    private void ResolveFieldPositionCollisionsForAllModes()
    {
        var settings = App.Services.GetRequiredService<SettingsService>();
        var orderedKeys = FieldElements().Select(f => f.Key).ToList();
        var anyChanged = false;

        foreach (var mode in AllModeNames)
        {
            var positions = settings.GetEntryFormFieldPositions(mode);
            var occupied = new HashSet<(int Row, int Position)>();

            foreach (var key in orderedKeys)
            {
                var cell = positions.TryGetValue(key, out var p) ? (p.Row, p.Position) : DefaultPositions[key];
                if (occupied.Add(cell)) continue;

                var freeCell = FindNextFreeCell(occupied);
                occupied.Add(freeCell);
                positions[key] = new EntryFormFieldPosition(freeCell.Row, freeCell.Position);
                anyChanged = true;
            }
        }

        if (anyChanged) settings.SaveEntryFormFieldPositions();
    }

    private static (int Row, int Position) FindNextFreeCell(HashSet<(int Row, int Position)> occupied)
    {
        for (var row = 1; row <= 200; row++)
            for (var position = 1; position <= 5; position++)
                if (!occupied.Contains((row, position)))
                    return (row, position);

        throw new InvalidOperationException("Ran out of field-layout cells while resolving a collision.");
    }

    private void QsoEntryView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;

        _subscribedViewModel = e.NewValue as QsoEntryViewModel;

        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged += ViewModel_PropertyChanged;

        ApplyFieldLayout();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QsoEntryViewModel.SelectedEntryModeOption))
            ApplyFieldLayout();
    }

    // Reads the current mode's saved field positions (SettingsService.GetEntryFormFieldPositions, via
    // the ViewModel) and places each field in FieldsGrid accordingly. A field with no saved position
    // (never dragged in this mode) uses its DefaultPositions fallback -- unless that cell is contested
    // by some other field's *explicit* saved position (e.g. the operator dragged Op onto Qth's default
    // slot, then later checked Qth on in the Columns picker for the first time), in which case it's
    // reassigned to the nearest actually-free cell instead, computed fresh on every call rather than
    // saved, so it stays "fluid" until the operator deliberately drags it.
    //
    // This has to be a two-pass resolution, not a single pass over FieldElements() in declaration order:
    // a single pass would let an early-processed contested field claim whatever cell FindNextFreeCell
    // finds first, including a cell that's only "free" because a later-processed field hasn't reserved
    // its own (non-contested) default yet -- bumping that later field too, cascading further reshuffling
    // than the one real conflict warranted. So every field's natural cell (explicit or default) is
    // determined up front; only cells with more than one claimant go through FindNextFreeCell.
    private void ApplyFieldLayout()
    {
        if (_subscribedViewModel is null) return;
        var saved = _subscribedViewModel.GetEntryFormFieldPositions();

        var natural = new Dictionary<string, (int Row, int Position)>();
        foreach (var key in FieldElements().Select(f => f.Key))
            natural[key] = saved.TryGetValue(key, out var p) ? (p.Row, p.Position) : DefaultPositions[key];

        var occupied = new HashSet<(int Row, int Position)>();
        var resolved = new Dictionary<string, (int Row, int Position)>();
        var needsReassignment = new List<string>();

        foreach (var group in natural.GroupBy(kv => kv.Value, kv => kv.Key))
        {
            var claimants = group.ToList();
            if (claimants.Count == 1)
            {
                resolved[claimants[0]] = group.Key;
                occupied.Add(group.Key);
                continue;
            }

            // A saved (explicit) position always wins a conflict over a field still sitting on its
            // default; the drop handler and the startup self-heal both already prevent two explicit
            // positions from ever colliding with each other, so at most one claimant here has one.
            var winner = claimants.FirstOrDefault(k => saved.ContainsKey(k)) ?? claimants[0];
            resolved[winner] = group.Key;
            occupied.Add(group.Key);
            needsReassignment.AddRange(claimants.Where(k => k != winner));
        }

        // Losers are reassigned in their own natural row/position order (not FieldElements() declaration
        // order) so "the next available slot" reads the same way scanning the grid top-to-bottom,
        // left-to-right would.
        foreach (var key in needsReassignment.OrderBy(k => natural[k].Row).ThenBy(k => natural[k].Position))
        {
            var cell = FindNextFreeCell(occupied);
            occupied.Add(cell);
            resolved[key] = cell;
        }

        foreach (var (key, element) in FieldElements())
        {
            var cell = resolved[key];
            Grid.SetRow(element, cell.Row - 1);
            Grid.SetColumn(element, cell.Position - 1);
        }
    }

    private IEnumerable<(string Key, FrameworkElement Element)> FieldElements()
    {
        yield return ("Station", StationField);
        yield return ("Callsign", CallsignField);
        yield return ("UtcTime", UtcTimeField);
        yield return ("Band", BandField);
        yield return ("Freq", FreqField);
        yield return ("Mode", ModeField);
        yield return ("SubMode", SubModeField);
        yield return ("LocalTime", LocalTimeField);
        yield return ("TimeOff", TimeOffField);
        yield return ("RstSent", RstSentField);
        yield return ("RstRcvd", RstRcvdField);
        yield return ("Name", NameField);
        yield return ("Grid", GridField);
        yield return ("City", CityField);
        yield return ("State", StateField);
        yield return ("County", CountyField);
        yield return ("Country", CountryField);
        yield return ("ArrlSection", ArrlSectionField);
        yield return ("CqZone", CqZoneField);
        yield return ("ItuZone", ItuZoneField);
        yield return ("Comment", CommentField);
        yield return ("Op", OpField);
        yield return ("TxPower", TxPowerField);
        yield return ("Qth", QthField);
        yield return ("FreqRx", FreqRxField);
        yield return ("Continent", ContinentField);
        yield return ("MyGrid", MyGridField);
        yield return ("MyState", MyStateField);
        yield return ("MyCounty", MyCountyField);
        yield return ("QslSent", QslSentField);
        yield return ("QslRcvd", QslRcvdField);
        yield return ("LotwQslSent", LotwQslSentField);
        yield return ("LotwQslRcvd", LotwQslRcvdField);
        yield return ("Skcc", SkccField);
        yield return ("MySkcc", MySkccField);
        yield return ("Precedence", PrecedenceField);
        yield return ("Check", CheckField);
        yield return ("Class", ClassField);
        yield return ("MySota", MySotaField);
        yield return ("Sota", SotaField);
        yield return ("MyPota", MyPotaField);
        yield return ("Pota", PotaField);
        yield return ("Sequence", SequenceField);
        yield return ("QslVia", QslViaField);
    }

    private Dictionary<FrameworkElement, string> KeyByElement =>
        _keyByElement ??= FieldElements().ToDictionary(f => f.Element, f => f.Key);

    // Stage 3: drag-and-drop rearrangement. A drag can only start from a field's label/background, never
    // from inside its TextBox/ComboBox/PasswordBox (see IsInputControl) -- otherwise clicking into a
    // field to type or open a dropdown would get hijacked into a reorder gesture instead.
    private void FieldsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (IsInputControl(source))
        {
            _dragStartPoint = null;
            _dragCandidateKey = null;
            return;
        }

        _dragStartPoint = e.GetPosition(FieldsGrid);
        _dragCandidateKey = FindFieldKeyAt(source);
    }

    private void FieldsGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStartPoint is null || _dragCandidateKey is null)
            return;

        var pos = e.GetPosition(FieldsGrid);
        if (Math.Abs(pos.X - _dragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var key = _dragCandidateKey;
        _dragStartPoint = null;
        _dragCandidateKey = null;
        DragDrop.DoDragDrop(FieldsGrid, new DataObject(FieldDragFormat, key), DragDropEffects.Move);
    }

    private void FieldsGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(FieldDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    // Dropping onto another field swaps the two fields' positions; dropping onto empty space just moves
    // the dragged field to that row/column. Both are persisted immediately via
    // QsoEntryViewModel.SetEntryFormFieldPosition (per-mode, mirrors SettingsService's existing
    // mutate-then-save idiom), then the grid re-renders from that saved state.
    private void FieldsGrid_Drop(object sender, DragEventArgs e)
    {
        if (_subscribedViewModel is null || !e.Data.GetDataPresent(FieldDragFormat)) return;
        var draggedKey = (string)e.Data.GetData(FieldDragFormat);

        var hitKey = FindFieldKeyAt(e.OriginalSource as DependencyObject);
        var targetCell = hitKey is not null ? GetElementCell(hitKey) : PixelToCell(e.GetPosition(FieldsGrid));

        // If nothing was visually under the cursor, the drop point's cell might still belong to a
        // currently-hidden field -- Visibility=Collapsed fields keep their Grid.Row/Column (see
        // ApplyFieldLayout), so an empty-looking spot isn't necessarily a free one. Checking by resolved
        // cell (not just hit-testing) is what stops a drop from ever silently landing two fields on the
        // same slot.
        var targetKey = hitKey ?? FindKeyAtCell(targetCell.Row, targetCell.Position);

        if (targetKey is not null && targetKey != draggedKey)
        {
            var draggedCell = GetElementCell(draggedKey);
            _subscribedViewModel.SetEntryFormFieldPosition(draggedKey, targetCell.Row, targetCell.Position);
            _subscribedViewModel.SetEntryFormFieldPosition(targetKey, draggedCell.Row, draggedCell.Position);
        }
        else
        {
            _subscribedViewModel.SetEntryFormFieldPosition(draggedKey, targetCell.Row, targetCell.Position);
        }

        ApplyFieldLayout();
    }

    private (int Row, int Position) GetElementCell(string key)
    {
        var element = KeyByElement.First(kv => kv.Value == key).Key;
        return (Grid.GetRow(element) + 1, Grid.GetColumn(element) + 1);
    }

    // Whichever field (visible or hidden) currently resolves to this exact cell, if any -- see
    // FieldsGrid_Drop.
    private string? FindKeyAtCell(int row, int position)
    {
        foreach (var (key, element) in FieldElements())
            if (Grid.GetRow(element) + 1 == row && Grid.GetColumn(element) + 1 == position)
                return key;
        return null;
    }

    // Row is found by walking RowDefinitions' actual (post-layout) heights until the cumulative height
    // passes the drop point's Y -- rows are Auto-sized (with a MinHeight floor, see the XAML), not evenly
    // spaced, so this can't be a simple division like the column calculation below.
    private (int Row, int Position) PixelToCell(Point point)
    {
        double cumulativeY = 0;
        int rowIndex = FieldsGrid.RowDefinitions.Count - 1;
        for (var i = 0; i < FieldsGrid.RowDefinitions.Count; i++)
        {
            cumulativeY += FieldsGrid.RowDefinitions[i].ActualHeight;
            if (point.Y < cumulativeY)
            {
                rowIndex = i;
                break;
            }
        }

        var columnCount = FieldsGrid.ColumnDefinitions.Count;
        var columnWidth = FieldsGrid.ActualWidth / columnCount;
        var columnIndex = columnWidth > 0 ? (int)(point.X / columnWidth) : 0;
        columnIndex = Math.Clamp(columnIndex, 0, columnCount - 1);

        return (rowIndex + 1, columnIndex + 1);
    }

    private string? FindFieldKeyAt(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement fe && KeyByElement.TryGetValue(fe, out var key))
                return key;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static bool IsInputControl(DependencyObject? source)
    {
        var current = source;
        while (current is not null && current is not Grid { Name: "FieldsGrid" })
        {
            if (current is Control) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
