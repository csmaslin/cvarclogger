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
    // Fallback row/position (1-based) for any field the operator hasn't dragged yet -- matches the
    // hand-tuned layout the GUI redesign settled on (6 fields max per row, widened from 5 to make room
    // for another field per row -- see FieldsGrid.ColumnDefinitions in the XAML). The persisted-override
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

    // Per-mode default layout, baked in from the developer's own live drag-arranged positions (extracted
    // from settings.json's EntryFormFieldPositionsByMode) so new users/fresh installs start from a
    // refined arrangement instead of the generic grid-order defaults above -- confirmed with the operator
    // before shipping as-is despite looking scattered (heavy drag-testing during development, not a
    // deliberately "tidy" layout, but the one they wanted). Only covers fields actually repositioned in
    // that mode; anything a mode doesn't list here falls through to DefaultPositions above. Net had
    // nothing to extract (never dragged), so it reuses Normal's arrangement instead of a distinct one.
    private static readonly Dictionary<string, Dictionary<string, (int Row, int Position)>> ModeDefaultPositions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Normal"] = new()
        {
            ["Band"] = (2, 1), ["Name"] = (1, 3), ["SubMode"] = (4, 1), ["Mode"] = (2, 2), ["RstSent"] = (2, 4),
            ["LotwQslSent"] = (5, 5), ["LotwQslRcvd"] = (6, 3), ["County"] = (3, 6), ["ArrlSection"] = (4, 3),
            ["FreqRx"] = (5, 1), ["City"] = (3, 2), ["Grid"] = (3, 1), ["TxPower"] = (2, 5), ["MyPota"] = (7, 4),
            ["MySota"] = (7, 3), ["Precedence"] = (6, 6), ["Check"] = (7, 1), ["Class"] = (7, 2), ["Comment"] = (1, 4),
            ["LocalTime"] = (2, 6), ["Callsign"] = (1, 2), ["TimeOff"] = (4, 6), ["UtcTime"] = (1, 6), ["Freq"] = (1, 5),
            ["MyGrid"] = (5, 4), ["Op"] = (5, 6), ["MySkcc"] = (4, 5), ["ItuZone"] = (9, 1), ["MyState"] = (3, 3),
            ["State"] = (6, 5), ["RstRcvd"] = (2, 3), ["CqZone"] = (4, 2), ["MyCounty"] = (3, 4), ["Country"] = (3, 5),
            ["Qth"] = (4, 4), ["Continent"] = (5, 2), ["Skcc"] = (6, 1), ["QslSent"] = (6, 2), ["QslRcvd"] = (6, 4),
            ["QslVia"] = (7, 5), ["Sota"] = (7, 6), ["Pota"] = (5, 3), ["Sequence"] = (9, 2),
        },
        // Net has never been dragged (0 saved positions in settings.json -- nothing to extract), so it
        // starts from the same complete, collision-free arrangement as Normal rather than the sparser
        // generic DefaultPositions fallback. Independent copy, not a shared reference, so the two can
        // diverge later without surprise.
        ["Net"] = new()
        {
            ["Band"] = (2, 1), ["Name"] = (1, 3), ["SubMode"] = (4, 1), ["Mode"] = (2, 2), ["RstSent"] = (2, 4),
            ["LotwQslSent"] = (5, 5), ["LotwQslRcvd"] = (6, 3), ["County"] = (3, 6), ["ArrlSection"] = (4, 3),
            ["FreqRx"] = (5, 1), ["City"] = (3, 2), ["Grid"] = (3, 1), ["TxPower"] = (2, 5), ["MyPota"] = (7, 4),
            ["MySota"] = (7, 3), ["Precedence"] = (6, 6), ["Check"] = (7, 1), ["Class"] = (7, 2), ["Comment"] = (1, 4),
            ["LocalTime"] = (2, 6), ["Callsign"] = (1, 2), ["TimeOff"] = (4, 6), ["UtcTime"] = (1, 6), ["Freq"] = (1, 5),
            ["MyGrid"] = (5, 4), ["Op"] = (5, 6), ["MySkcc"] = (4, 5), ["ItuZone"] = (9, 1), ["MyState"] = (3, 3),
            ["State"] = (6, 5), ["RstRcvd"] = (2, 3), ["CqZone"] = (4, 2), ["MyCounty"] = (3, 4), ["Country"] = (3, 5),
            ["Qth"] = (4, 4), ["Continent"] = (5, 2), ["Skcc"] = (6, 1), ["QslSent"] = (6, 2), ["QslRcvd"] = (6, 4),
            ["QslVia"] = (7, 5), ["Sota"] = (7, 6), ["Pota"] = (5, 3), ["Sequence"] = (9, 2),
        },
        ["Contest"] = new()
        {
            ["Continent"] = (3, 2), ["UtcTime"] = (6, 3), ["Comment"] = (2, 2), ["TxPower"] = (1, 6),
            ["TimeOff"] = (5, 5), ["Skcc"] = (2, 1), ["RstSent"] = (8, 1), ["Grid"] = (1, 5), ["Band"] = (4, 5),
            ["Sequence"] = (2, 6), ["ArrlSection"] = (2, 5), ["SubMode"] = (3, 1), ["Mode"] = (1, 4),
            ["Precedence"] = (2, 4), ["Class"] = (2, 3), ["LocalTime"] = (8, 5), ["Freq"] = (1, 3),
        },
        ["Sota"] = new()
        {
            ["RstSent"] = (4, 1), ["RstRcvd"] = (4, 2), ["Continent"] = (7, 5), ["County"] = (5, 5), ["Check"] = (5, 4),
            ["Op"] = (3, 5), ["Class"] = (9, 1), ["TxPower"] = (4, 3), ["State"] = (7, 3), ["FreqRx"] = (8, 1),
            ["ItuZone"] = (3, 1), ["LotwQslSent"] = (9, 4), ["Band"] = (9, 2), ["LotwQslRcvd"] = (2, 4),
            ["ArrlSection"] = (8, 5), ["CqZone"] = (9, 3), ["TimeOff"] = (5, 1), ["Comment"] = (2, 5), ["Name"] = (2, 2),
            ["City"] = (8, 3), ["LocalTime"] = (1, 4), ["SubMode"] = (2, 3), ["MyCounty"] = (3, 2), ["MyGrid"] = (3, 3),
            ["Grid"] = (10, 1), ["MyPota"] = (3, 4), ["MySkcc"] = (8, 4), ["MySota"] = (4, 5), ["Freq"] = (2, 1),
            ["MyState"] = (5, 3), ["UtcTime"] = (1, 5), ["Pota"] = (6, 1), ["Qth"] = (7, 1), ["Precedence"] = (6, 2),
            ["QslRcvd"] = (6, 3), ["QslSent"] = (6, 4), ["QslVia"] = (6, 5), ["Sequence"] = (5, 2), ["Skcc"] = (7, 2),
            ["Sota"] = (1, 3), ["Mode"] = (7, 4),
        },
        ["Pota"] = new()
        {
            ["Band"] = (2, 4), ["Name"] = (9, 3), ["Freq"] = (1, 3), ["LocalTime"] = (2, 6), ["Mode"] = (1, 4),
            ["SubMode"] = (9, 4), ["RstSent"] = (3, 2), ["RstRcvd"] = (6, 3), ["Comment"] = (1, 5), ["UtcTime"] = (1, 6),
            ["Continent"] = (2, 1), ["Pota"] = (2, 3), ["MySota"] = (5, 5), ["TxPower"] = (2, 2), ["MyPota"] = (2, 5),
        },
        ["All"] = new()
        {
            ["Name"] = (2, 4), ["Grid"] = (2, 5), ["Country"] = (3, 4), ["ArrlSection"] = (3, 5), ["Op"] = (4, 4),
            ["TxPower"] = (4, 5), ["MyGrid"] = (5, 4), ["MyState"] = (5, 5), ["MyCounty"] = (6, 4),
            ["LotwQslRcvd"] = (7, 1), ["LotwQslSent"] = (6, 5), ["Check"] = (8, 4), ["QslVia"] = (7, 4),
            ["Pota"] = (6, 6), ["MyPota"] = (5, 6), ["Sota"] = (4, 6), ["MySota"] = (3, 6), ["Class"] = (7, 5),
            ["Sequence"] = (8, 5),
        },
    };

    // Resolution order for a field with no explicit saved position in the current mode: this mode's own
    // baked-in default (ModeDefaultPositions) first, then the generic cross-mode fallback (DefaultPositions).
    private static (int Row, int Position) GetDefaultPosition(string mode, string key) =>
        ModeDefaultPositions.TryGetValue(mode, out var modeDefaults) && modeDefaults.TryGetValue(key, out var cell)
            ? cell
            : DefaultPositions[key];

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
            var occupied = new HashSet<(int Row, int Position)>();

            foreach (var key in orderedKeys)
            {
                var cell = positions.TryGetValue(key, out var p) ? (p.Row, p.Position) : GetDefaultPosition(mode, key);
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
            _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;

        _subscribedViewModel = e.NewValue as QsoEntryViewModel;

        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged += ViewModel_PropertyChanged;

        ApplyFieldLayout();
        FocusFirstField();
    }

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
        var mode = _subscribedViewModel.SelectedEntryModeOption.Value.ToString();

        var natural = new Dictionary<string, (int Row, int Position)>();
        foreach (var key in FieldElements().Select(f => f.Key))
            natural[key] = saved.TryGetValue(key, out var p) ? (p.Row, p.Position) : GetDefaultPosition(mode, key);

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

        // WPF's default Tab navigation follows each element's position in FieldsGrid.Children (i.e.
        // declaration order), not Grid.Row/Column -- so without this, Tab visibly jumps around once a
        // field's visual position no longer matches its declaration order (e.g. after a drag). An earlier
        // attempt set KeyboardNavigation.TabIndex per element instead of reordering Children directly;
        // that did not actually change Tab order in testing, so it's been removed rather than left in
        // place alongside this. Moving each element to the end of Children, visited in ascending
        // (Row, Position) order, re-sorts the whole collection in one pass -- Tab then visits fields left
        // to right, one row at a time, matching what's on screen.
        foreach (var (_, element) in FieldElements().OrderBy(f => resolved[f.Key].Row).ThenBy(f => resolved[f.Key].Position))
        {
            FieldsGrid.Children.Remove(element);
            FieldsGrid.Children.Add(element);
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
