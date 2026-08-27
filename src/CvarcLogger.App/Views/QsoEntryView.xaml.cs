using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CvarcLogger.App.Services;
using CvarcLogger.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CvarcLogger.App.Views;

public partial class QsoEntryView : UserControl
{
    // No alphabetical sorting: a field with no saved position simply takes the first genuinely free
    // cell (see FindNextFreeCell), scanning row 1 position 1, then 2..6, then row 2 position 1, and so
    // on. Whichever order fields happen to be checked on in the Columns/Tabs picker is the order they
    // fill in -- the first 6 checked land across row 1, the 7th starts row 2, etc. Once assigned, that
    // position is immediately persisted (see ApplyFieldLayout), exactly as if it had been dragged there,
    // so it never moves again just because some other field's visibility changed.

    // "Station" and "Callsign" have no Visibility binding in their XAML at all (see StationField/
    // CallsignField) -- they're unconditionally rendered regardless of what IsFieldVisible/
    // GetHiddenColumns says. That matters because "Station" collides with the *log grid's* own
    // "Station" column key (Station Callsign column, defaults to hidden) -- the two share a settings
    // key by coincidence, not by design, so IsFieldVisible("Station") answers for the wrong control.
    // Without this override, the entry form's always-visible Station field would be wrongly treated as
    // hidden-and-unpositioned, crashing ApplyFieldLayout's later lookup with a KeyNotFoundException.
    private static readonly HashSet<string> AlwaysVisibleFieldKeys = new(StringComparer.OrdinalIgnoreCase) { "Station", "Callsign" };

    // RstSent/RstRcvd, QslSent/QslRcvd, and LotwQslSent/LotwQslRcvd are each two separate draggable
    // fields sharing ONE visibility checkbox in the Columns/Tabs picker (see ShowRstField/ShowQslField/
    // ShowLotwField in QsoEntryViewModel and the matching XAML comment on QslSentField) -- the checkbox's
    // actual settings key is "Rst"/"Qsl"/"Lotw", not the individual field's own position key. Checking
    // IsFieldVisible("RstSent") directly (its position key) always answers "visible", regardless of the
    // checkbox, since "RstSent" itself is never in any hidden set -- only "Rst" is. This maps each such
    // field's position key to the key that actually governs its visibility.
    private static readonly Dictionary<string, string> VisibilityCheckKeyOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RstSent"] = "Rst", ["RstRcvd"] = "Rst",
        ["QslSent"] = "Qsl", ["QslRcvd"] = "Qsl",
        ["LotwQslSent"] = "Lotw", ["LotwQslRcvd"] = "Lotw",
    };

    private bool IsFieldActuallyVisible(string positionKey) =>
        AlwaysVisibleFieldKeys.Contains(positionKey) ||
        _subscribedViewModel!.IsFieldVisible(VisibilityCheckKeyOverrides.TryGetValue(positionKey, out var visKey) ? visKey : positionKey);

    // Custom drag data format, distinct from the default string format -- a plain string format would
    // collide with WPF's own built-in "drag selected text out of a TextBox" gesture, which also carries a
    // string payload. Field drags can now start from inside a TextBox/ComboBox too (see
    // FieldsGrid_PreviewMouseLeftButtonDown), so this distinct format is what keeps the Drop handler from
    // ever mistaking one gesture for the other.
    private const string FieldDragFormat = "CvarcLogger.QsoEntryFieldKey";

    private QsoEntryViewModel? _subscribedViewModel;
    private Dictionary<FrameworkElement, string>? _keyByElement;
    private Point? _dragStartPoint;
    private string? _dragCandidateKey;

    // Every mode that has its own saved field-position map, independent of whether it's reachable from
    // the sidebar yet (Net isn't -- see QsoEntryModeOptions -- but still gets a settings slot).
    private static readonly string[] AllModeNames =
        { "Normal", "Contest", "Sota", "Pota", "Net", "Custom1", "Custom2", "Custom3", "All" };

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
            var hidden = settings.GetHiddenColumns(mode);

            bool IsVisibleInMode(string key) =>
                AlwaysVisibleFieldKeys.Contains(key) ||
                !hidden.Contains(VisibilityCheckKeyOverrides.TryGetValue(key, out var visKey) ? visKey : key);

            // Only checking for collisions among fields that are BOTH visible in this mode AND already
            // have a saved position -- an unsaved field doesn't need (or get) a default reserved here at
            // all anymore; it's assigned lazily, the first genuinely free cell, whenever ApplyFieldLayout
            // actually renders that mode (see there). Nothing to precompute across all modes up front.
            var occupied = new HashSet<(int Row, int Position)>();

            foreach (var key in orderedKeys)
            {
                if (!IsVisibleInMode(key) || !positions.TryGetValue(key, out var p)) continue;

                var cell = (p.Row, p.Position);
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
            for (var position = 1; position <= 6; position++)
                if (!occupied.Contains((row, position)))
                    return (row, position);

        throw new InvalidOperationException("Ran out of field-layout cells while resolving a collision.");
    }

    private void QsoEntryView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _subscribedViewModel.FieldVisibilityChanged -= OnFieldVisibilityChanged;
        }

        _subscribedViewModel = e.NewValue as QsoEntryViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += ViewModel_PropertyChanged;
            _subscribedViewModel.FieldVisibilityChanged += OnFieldVisibilityChanged;
        }

        ApplyFieldLayout();
        FocusFirstField();
    }

    // Checking a field on/off in the Columns/Tabs picker only flips its bound Visibility -- it never
    // touched Grid.Row/Column before this, so a newly-shown field just revealed whatever stale cell it
    // was already sitting on (assigned the last time ApplyFieldLayout ran, e.g. at startup), instead of
    // taking its correct place in the now-changed set of visible fields. Re-running the full layout here
    // is what makes the alphabetical fallback (and collision resolution generally) actually apply live
    // as fields are toggled, not just at the next mode switch or app restart.
    private void OnFieldVisibilityChanged(object? sender, EventArgs e) => ApplyFieldLayout();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QsoEntryViewModel.SelectedEntryModeOption))
        {
            ApplyFieldLayout();
            FocusFirstField();
        }
    }

    // Gives Tab navigation a predictable starting point on first load and on every mode switch (a fresh
    // set of visible fields deserves a fresh starting point) -- otherwise nothing has focus until the
    // operator clicks somewhere first. Doesn't run on every drag-drop reposition (FieldsGrid_Drop calls
    // ApplyFieldLayout too, deliberately not this) since stealing focus right after a drag would be
    // disruptive. Deferred to Background priority because the just-applied Grid.Row/Column/Visibility
    // changes haven't been through a layout pass yet when this is called -- MoveFocus needs the target
    // to already be arranged, or it silently no-ops.
    private void FocusFirstField()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var key = FindKeyAtCell(1, 1);
            var element = key is null ? null : FieldElements().FirstOrDefault(f => f.Key == key).Element;
            element?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }), DispatcherPriority.Background);
    }

    // Enter logs the QSO from anywhere on the form, not just the Callsign field -- LogQsoCommand's own
    // CanExecute/validation already requires a non-empty Callsign (see QsoEntryViewModel.LogQsoAsync), so
    // that's the only gate needed here. Wired as a plain (bubbling) KeyDown on the UserControl root rather
    // than a UserControl.InputBindings/KeyBinding, so it fires *after* whichever control currently has
    // focus handles Enter itself first -- an editable ComboBox (Band/Mode/Sub-Mode) with its dropdown open
    // closes the dropdown on the first Enter and only lets a second Enter reach here, a known/accepted
    // quirk (see memory). Text bindings default to UpdateSourceTrigger=LostFocus, which never fires here
    // since focus never actually leaves the currently-focused box on Enter -- UpdateSource() flushes
    // whatever's currently focused into the ViewModel first so LogQsoCommand sees it instead of whatever
    // was there before this keystroke, generalizing what used to be a Callsign-only fix to every field.
    private void QsoEntryView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (Keyboard.FocusedElement is TextBox focused)
            focused.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

        if (_subscribedViewModel?.LogQsoCommand.CanExecute(null) != true) return;
        _subscribedViewModel.LogQsoCommand.Execute(null);
        e.Handled = true;
    }

    // Tab out of the Callsign field triggers automatic lookup, so users don't have to click the Lookup button
    private void CallsignField_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || _subscribedViewModel is null) return;

        // Update the binding first so the ViewModel sees the typed callsign
        if (sender is TextBox callsignBox)
            callsignBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

        // Trigger the lookup command
        if (_subscribedViewModel.LookupCommand.CanExecute(null))
        {
            _subscribedViewModel.LookupCommand.Execute(null);
            e.Handled = false; // Allow Tab to move focus to the next field after lookup completes
        }
    }

    // Reads the current mode's saved field positions (SettingsService.GetEntryFormFieldPositions, via
    // the ViewModel) and places each field in FieldsGrid accordingly. Two categories only:
    //   - A field with a saved position (dragged, or previously auto-assigned -- see below) always
    //     keeps that exact cell. Nothing ever moves it except a fresh drag.
    //   - A visible field with NO saved position gets the first genuinely free cell, scanning row 1
    //     position 1, then 2..6, then row 2 position 1, and so on (FindNextFreeCell) -- no alphabetical
    //     sorting, no ranking among "what else needs a default": literally whichever cell is empty
    //     first. That position is then immediately persisted, exactly as if it had been dragged there,
    //     so a later, unrelated field being checked on can never reshuffle it again.
    //
    // A field currently hidden (Visibility=Collapsed) never occupies a cell at all here, regardless of
    // whether it has a saved position -- its saved position (if any) is left untouched in settings for
    // whenever it's shown again, but while hidden it can't block a visible field from using that cell.
    private const int HiddenCellRow = 999;

    private void ApplyFieldLayout()
    {
        if (_subscribedViewModel is null) return;
        var saved = _subscribedViewModel.GetEntryFormFieldPositions();
        var allKeys = FieldElements().Select(f => f.Key).ToList();

        var resolved = new Dictionary<string, (int Row, int Position)>();
        var hiddenUnset = new List<string>();
        var occupied = new HashSet<(int Row, int Position)>();
        var needsAssignment = new List<string>();

        // First pass: every visible field with an existing saved position claims it outright. Saved
        // positions never collide with each other in practice (FieldsGrid_Drop's swap logic and the
        // startup self-heal both prevent it), so there's no need for the old two-pass collision dance.
        foreach (var key in allKeys)
        {
            if (!IsFieldActuallyVisible(key)) { hiddenUnset.Add(key); continue; }
            if (saved.TryGetValue(key, out var p))
            {
                resolved[key] = (p.Row, p.Position);
                occupied.Add((p.Row, p.Position));
            }
            else
            {
                needsAssignment.Add(key);
            }
        }

        // Second pass: any visible field with no saved position gets the next free cell, in
        // FieldElements() declaration order. In practice only one field at a time transitions from
        // hidden to visible (one checkbox click = one ApplyFieldLayout call), so this order rarely
        // matters; it only affects the rare case of several fields becoming visible simultaneously
        // (e.g. a mode's first-ever render), which just needs to be deterministic, not meaningful.
        foreach (var key in needsAssignment)
        {
            var cell = FindNextFreeCell(occupied);
            occupied.Add(cell);
            resolved[key] = cell;
            _subscribedViewModel.SetEntryFormFieldPosition(key, cell.Row, cell.Position);
        }

        foreach (var (key, element) in FieldElements())
        {
            if (hiddenUnset.Contains(key))
            {
                // Collapsed, so never rendered -- exact cell doesn't matter, it just must never be
                // mistaken for occupying a real, visible slot (see FieldsGrid_Drop's collision check).
                Grid.SetRow(element, HiddenCellRow);
                Grid.SetColumn(element, 0);
                continue;
            }
            var cell = resolved[key];
            Grid.SetRow(element, cell.Row - 1);
            Grid.SetColumn(element, cell.Position - 1);
        }

        // WPF's default Tab navigation follows each element's position in FieldsGrid.Children (i.e.
        // declaration order), not Grid.Row/Column -- so without this, Tab visibly jumps around once a
        // field's visual position no longer matches its declaration order (e.g. after a drag). An earlier
        // attempt set KeyboardNavigation.TabIndex per element instead of reordering Children directly;
        // that did not actually change Tab order in testing, so it's been removed rather than left in
        // place alongside this. Moving each element to the end of Children, visited in ascending
        // (Row, Position) order, re-sorts the whole collection in one pass -- Tab then visits fields left
        // to right, one row at a time, matching what's on screen. Hidden-and-unset fields have no entry
        // in `resolved` (see above) -- they're Collapsed, so WPF's Tab navigation already skips them
        // regardless of where they land in Children, hence the arbitrary-but-safe HiddenCellRow fallback.
        foreach (var (_, element) in FieldElements()
                     .OrderBy(f => resolved.TryGetValue(f.Key, out var c) ? c.Row : HiddenCellRow)
                     .ThenBy(f => resolved.TryGetValue(f.Key, out var c) ? c.Position : 0))
        {
            FieldsGrid.Children.Remove(element);
            FieldsGrid.Children.Add(element);
        }

        // "Select columns/tabs needed" hint, shown only while this mode is a genuinely blank slate.
        // Station/Callsign are always visible (AlwaysVisibleFieldKeys) and so are excluded from the
        // count -- otherwise a fresh install would never be considered empty and the hint would never
        // appear at all. Re-evaluated on every layout pass, which already covers startup, mode switch,
        // and every visibility toggle.
        bool anyOptionalFieldVisible = allKeys.Any(k => !AlwaysVisibleFieldKeys.Contains(k) && IsFieldActuallyVisible(k));
        EmptyStateHint.Visibility = anyOptionalFieldVisible ? Visibility.Collapsed : Visibility.Visible;
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

    // Stage 3: drag-and-drop rearrangement. A drag can start anywhere on a field, including inside its
    // TextBox/ComboBox/PasswordBox -- not just the label, so the whole field acts as its own handle
    // rather than requiring the operator to aim for a thin label strip. This doesn't hijack ordinary
    // clicking/typing: PreviewMouseMove below only calls DoDragDrop once the mouse has actually moved
    // past the OS's own minimum drag distance, so a plain click still focuses the field, positions the
    // text cursor, or opens a dropdown exactly as before -- only a genuine drag gesture reroutes into a
    // reorder instead of (for a TextBox) starting a text selection.
    private void FieldsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Lock Fields (see QsoEntryViewModel.LockFieldsForCurrentMode) stops a drag from ever starting
        // for the current mode -- leaving _dragCandidateKey null makes PreviewMouseMove below a no-op
        // regardless of how far the mouse then moves, without touching normal click/focus/type behavior.
        if (_subscribedViewModel?.LockFieldsForCurrentMode == true)
        {
            _dragStartPoint = null;
            _dragCandidateKey = null;
            return;
        }

        var source = e.OriginalSource as DependencyObject;
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
}
