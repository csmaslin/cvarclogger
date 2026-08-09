using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Models;
using CvarcLogger.Core.Rig;
using CvarcLogger.Core.UiStandards;
using Serilog;

namespace CvarcLogger.App.ViewModels;

public partial class QsoEntryViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly IStationProfileRepository _stationProfileRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly IGridZoneResolver _gridZoneResolver;
    private readonly LookupCoordinator _lookupCoordinator;
    private readonly RigControlCoordinator _rigCoordinator;
    private readonly InternetCatCoordinator _internetCat;
    private readonly SettingsService _settings;
    private readonly DialogService _dialogService;
    private readonly IClock _clock;
    private readonly GridTrackerBroadcastService _gridTrackerBroadcast;
    private readonly SotaRefDatabase _sotaRefDb;
    private readonly PotaRefDatabase _potaRefDb;
    private readonly DispatcherTimer _catPollTimer;
    private readonly DispatcherTimer _liveClockTimer;
    private string? _lastLookedUpCallsign;

    /// <summary>Which CAT source the current connection actually uses, captured at connect time so a later
    /// toggle of the Internet Control enable setting can't make Disconnect/Poll target the wrong backend
    /// mid-session. True = network K4 (InternetCatCoordinator); false = Hamlib (RigControlCoordinator).</summary>
    private bool _connectedViaInternet;

    /// <summary>Guards QsoDateTimeUtcText/QsoDateTimeLocalText's bidirectional sync (see
    /// OnQsoDateTimeUtcTextChanged/OnQsoDateTimeLocalTextChanged) against re-entrant feedback -- setting
    /// one field programmatically from the other must not bounce back and forth.</summary>
    private bool _isSyncingDateTime;

    /// <summary>Set around _liveClockTimer's own writes to QsoDateTimeUtcText so the change handler below
    /// can tell "the clock ticked" apart from "the operator typed a digit" -- only the latter should stop
    /// the live clock.</summary>
    private bool _isLiveClockUpdate;

    /// <summary>True once the operator has typed into the UTC date/time field, or pressed "Start Time"
    /// (see SetStartTime), for the QSO currently being entered. Stops _liveClockTimer from overwriting a
    /// manually-entered/frozen UTC time; reset back to false in InitializeAsync/ResetForNextQso so the
    /// next QSO starts live-ticking again.</summary>
    private bool _dateTimeManuallyEdited;

    /// <summary>Same idea as _dateTimeManuallyEdited but for Local Time specifically. Kept separate so
    /// "Start Time" freezing the UTC field doesn't also freeze Local -- once UTC stops advancing,
    /// OnLiveClockTick switches to ticking Local directly (see there) until the operator types into it
    /// by hand.</summary>
    private bool _localTimeManuallyEdited;

    // Includes seconds (unlike QsoEditViewModel's minute-only format) so the live clock's ticking is
    // visibly obvious at a glance, rather than requiring up to a full minute's wait to see it move.
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    // A manually-typed time doesn't need seconds -- DateInputMask only renders as many digits as were
    // typed, so stopping after the minute digits (not typing the last two) naturally produces a string
    // in this shorter shape.
    private const string ShortDateTimeFormat = "yyyy-MM-dd HH:mm";
    private static readonly string[] AcceptedDateTimeFormats = { DateTimeFormat, ShortDateTimeFormat };

    /// <summary>Strips a dangling trailing separator DateInputMask.Render can leave behind. Render emits
    /// a format string's literal characters (the ':' between mm and ss here) as soon as the digit group
    /// before them is complete, regardless of whether any digits still follow -- by design, so typing the
    /// 4th year digit immediately shows "2026-". That same rule means stopping at exactly the 12 minute
    /// digits (no seconds) renders "...HH:mm:" with a trailing ':' that matches neither accepted format
    /// verbatim. Trimming it before parsing is what actually makes seconds-optional entry work.</summary>
    private static string TrimDanglingSeparator(string value) => value.TrimEnd(':', '-', ' ');

    private static bool TryParseQsoDateTime(string value, out DateTime result) =>
        DateTime.TryParseExact(TrimDanglingSeparator(value), AcceptedDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

    /// <summary>Which format to render a synced/derived field in: full seconds precision if the source
    /// text the sync is based on has it, otherwise the shorter no-seconds shape -- keeps a manually-typed
    /// (seconds-free) entry from sprouting a ":00" in the other field it auto-fills.</summary>
    private static string SyncFormatFor(string sourceText) =>
        DateTime.TryParseExact(TrimDanglingSeparator(sourceText), DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? DateTimeFormat
            : ShortDateTimeFormat;

    public event EventHandler? QsoLogged;
    public event EventHandler<string>? CallsignChanged;

    /// <summary>Raised when the Choose Columns picker's checkboxes change, so the entry form's own
    /// fields can hide/show in step with the log grid's columns (see QsoEntryView's field-visibility
    /// wiring). Bridged from QsoLogViewModel.ColumnVisibilityChanged via MainViewModel, rather than
    /// this view model depending on its sibling directly.</summary>
    public event EventHandler? FieldVisibilityChanged;

    public void NotifyFieldVisibilityChanged()
    {
        FieldVisibilityChanged?.Invoke(this, EventArgs.Empty);
        // Every column-gated *Field property is binding-driven now (see the comment above ShowSkccField),
        // so each one has to be re-evaluated here when a column is toggled in the Choose Columns picker --
        // there's no more code-behind fallback doing this for any of them.
        OnPropertyChanged(nameof(ShowSkccField));
        OnPropertyChanged(nameof(ShowMySkccField));
        OnPropertyChanged(nameof(ShowPrecedenceField));
        OnPropertyChanged(nameof(ShowCheckField));
        OnPropertyChanged(nameof(ShowClassField));
        OnPropertyChanged(nameof(ShowTimeOffField));
        OnPropertyChanged(nameof(ShowGridField));
        OnPropertyChanged(nameof(ShowCityField));
        OnPropertyChanged(nameof(ShowStateField));
        OnPropertyChanged(nameof(ShowCountyField));
        OnPropertyChanged(nameof(ShowCountryField));
        OnPropertyChanged(nameof(ShowArrlSectionField));
        OnPropertyChanged(nameof(ShowCqZoneField));
        OnPropertyChanged(nameof(ShowItuZoneField));
        OnPropertyChanged(nameof(ShowMySotaField));
        OnPropertyChanged(nameof(ShowSotaField));
        OnPropertyChanged(nameof(ShowMyPotaField));
        OnPropertyChanged(nameof(ShowPotaField));
        OnPropertyChanged(nameof(ShowOpField));
        OnPropertyChanged(nameof(ShowQthField));
        OnPropertyChanged(nameof(ShowTxPowerField));
        OnPropertyChanged(nameof(ShowCommentField));
        OnPropertyChanged(nameof(ShowFreqRxField));
        OnPropertyChanged(nameof(ShowQslField));
        OnPropertyChanged(nameof(ShowLotwField));
        OnPropertyChanged(nameof(ShowMyCountyField));
        OnPropertyChanged(nameof(ShowContinentField));
        OnPropertyChanged(nameof(ShowMyGridField));
        OnPropertyChanged(nameof(ShowMyStateField));
        OnPropertyChanged(nameof(ShowSequenceField));
        OnPropertyChanged(nameof(ShowModeField));
        OnPropertyChanged(nameof(ShowSubModeField));
        OnPropertyChanged(nameof(ShowUtcTimeField));
        OnPropertyChanged(nameof(ShowStartTimeField));
        OnPropertyChanged(nameof(ShowEndTimeField));
        OnPropertyChanged(nameof(ShowNameField));
        OnPropertyChanged(nameof(ShowLocalTimeField));
        OnPropertyChanged(nameof(ShowBandField));
        OnPropertyChanged(nameof(ShowFreqField));
        OnPropertyChanged(nameof(ShowRstField));
        OnPropertyChanged(nameof(ShowQslViaField));
        OnPropertyChanged(nameof(ShowRow3));
    }

    /// <summary>Whether the entry-form field for this column key should be shown, mirroring
    /// QsoLogViewModel.IsColumnVisible's own logic against the same underlying setting. Only ever
    /// consulted for optional/supplementary fields (see QsoEntryView.xaml.cs) -- Callsign, Station,
    /// Date/Time, Local Time, Band, and Mode are always shown regardless, since those are needed to
    /// log any QSO at all.</summary>
    public bool IsFieldVisible(string key) => !_settings.GetHiddenColumns(SelectedEntryModeOption.Value.ToString()).Contains(key);

    /// <summary>Saved field row/position layout for the currently selected Log Entry Mode -- read by
    /// QsoEntryView's code-behind to place each field's Grid.Row/Grid.Column (see
    /// EntryFormFieldPosition, SettingsService.GetEntryFormFieldPositions). Independent per mode; a
    /// field missing from the returned map hasn't been dragged yet, so the View falls back to its own
    /// hardcoded default position for it.</summary>
    public IReadOnlyDictionary<string, EntryFormFieldPosition> GetEntryFormFieldPositions() =>
        _settings.GetEntryFormFieldPositions(SelectedEntryModeOption.Value.ToString());

    /// <summary>Persists a field's new row/position for the currently selected mode -- called by
    /// QsoEntryView's drag-and-drop drop handler once the operator releases a dragged field over its
    /// new slot. Overwrites any existing saved position for the same key.</summary>
    public void SetEntryFormFieldPosition(string key, int row, int position)
    {
        var positions = _settings.GetEntryFormFieldPositions(SelectedEntryModeOption.Value.ToString());
        positions[key] = new EntryFormFieldPosition(row, position);
        _settings.SaveEntryFormFieldPositions();
    }

    [ObservableProperty] private string callsign = string.Empty;
    [ObservableProperty] private string qsoDateTimeUtcText = string.Empty;
    [ObservableProperty] private string qsoDateTimeLocalText = string.Empty;
    [ObservableProperty] private string? qsoDateTimeOffUtcText;
    [ObservableProperty] private string band = "20m";
    [ObservableProperty] private string mode = "SSB";
    [ObservableProperty] private string? frequencyMhz;
    [ObservableProperty] private string? rstSent = "59";
    [ObservableProperty] private string? rstRcvd = "59";
    [ObservableProperty] private string? name;
    [ObservableProperty] private string? gridSquare;
    [ObservableProperty] private string? city;
    [ObservableProperty] private string? state;
    [ObservableProperty] private string? county;
    [ObservableProperty] private string? country;
    [ObservableProperty] private string? arrlSection;
    [ObservableProperty] private string? cqZone;
    [ObservableProperty] private string? ituZone;
    // MySotaRef/MySigInfo are sticky (not cleared in ResetForNextQso), same rationale as Band/Mode/
    // RstSent/RstRcvd -- the operator's own summit/park stays constant across a whole activation
    // session. SotaRef/SigInfo describe the *contacted* station, so they reset per QSO like Name/Grid.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMySotaRefValid))]
    [NotifyPropertyChangedFor(nameof(ShowMySotaRefWarning))]
    private string? mySotaRef;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSotaRefValid))]
    [NotifyPropertyChangedFor(nameof(ShowSotaRefWarning))]
    private string? sotaRef;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMySigInfoValid))]
    [NotifyPropertyChangedFor(nameof(ShowMySigInfoWarning))]
    private string? mySigInfo;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSigInfoValid))]
    [NotifyPropertyChangedFor(nameof(ShowSigInfoWarning))]
    private string? sigInfo;

    // Soft validation only -- a mismatch shows a warning label but never blocks Log QSO, since the regex
    // can't anticipate every real-world reference. The "Show...Warning" properties exist purely so XAML
    // doesn't need an inverse-bool-to-visibility converter.
    public bool IsMySotaRefValid => ReferenceFormatStandards.IsValidSotaRef(MySotaRef);
    public bool IsSotaRefValid => ReferenceFormatStandards.IsValidSotaRef(SotaRef);
    public bool IsMySigInfoValid => ReferenceFormatStandards.IsValidPotaRef(MySigInfo);
    public bool IsSigInfoValid => ReferenceFormatStandards.IsValidPotaRef(SigInfo);
    public Visibility ShowMySotaRefWarning => IsMySotaRefValid ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShowSotaRefWarning => IsSotaRefValid ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShowMySigInfoWarning => IsMySigInfoValid ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShowSigInfoWarning => IsSigInfoValid ? Visibility.Collapsed : Visibility.Visible;

    // Chaser-focused reference lookup: when the operator types a *contacted* station's SOTA/POTA
    // reference (SotaRef/SigInfo -- not the My... sticky variants, which describe the operator's own
    // activation and aren't looked up), resolve its name/detail from the local reference database (see
    // SotaRefDatabase/PotaRefDatabase, downloaded via the "Sota DB"/"Pota DB" sidebar buttons) and show
    // it in the same label spot the format warning above uses. The two are naturally mutually exclusive:
    // a lookup only runs once the format is already valid, so the warning is never visible at the same
    // time as a resolved name.
    [ObservableProperty] private string? sotaLookupText;
    [ObservableProperty] private string? potaLookupText;
    public Visibility ShowSotaLookupText => string.IsNullOrEmpty(SotaLookupText) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShowPotaLookupText => string.IsNullOrEmpty(PotaLookupText) ? Visibility.Collapsed : Visibility.Visible;

    // Force-uppercase as the operator types (not just at save time, like State/ArrlSection) -- both
    // ReferenceFormatStandards' regexes and the reference database's ref column are uppercase, and the
    // field updates live (UpdateSourceTrigger=PropertyChanged, see QsoEntryView.xaml) specifically so the
    // lookup can fire without a click-away, so lowercase input needs correcting immediately rather than
    // only at save. Setting the property back to its own uppercase form re-enters this same handler once
    // more, already uppercase, which is what actually triggers the lookup.
    partial void OnSotaRefChanged(string? value)
    {
        if (value is not null && value != value.ToUpperInvariant()) { SotaRef = value.ToUpperInvariant(); return; }
        _ = ResolveSotaRefAsync(value);
    }

    partial void OnSigInfoChanged(string? value)
    {
        if (value is not null && value != value.ToUpperInvariant()) { SigInfo = value.ToUpperInvariant(); return; }
        _ = ResolvePotaRefAsync(value);
    }

    private async Task ResolveSotaRefAsync(string? value)
    {
        SotaLookupText = null;
        OnPropertyChanged(nameof(ShowSotaLookupText));
        if (string.IsNullOrWhiteSpace(value) || !ReferenceFormatStandards.IsValidSotaRef(value)) return;

        var info = await _sotaRefDb.LookupAsync(value);
        if (value != SotaRef) return; // the field changed again while this lookup was in flight

        SotaLookupText = info?.Display;
        OnPropertyChanged(nameof(ShowSotaLookupText));
    }

    private async Task ResolvePotaRefAsync(string? value)
    {
        PotaLookupText = null;
        OnPropertyChanged(nameof(ShowPotaLookupText));
        if (string.IsNullOrWhiteSpace(value) || !ReferenceFormatStandards.IsValidPotaRef(value)) return;

        var info = await _potaRefDb.LookupAsync(value);
        if (value != SigInfo) return; // the field changed again while this lookup was in flight

        PotaLookupText = info?.Display;
        OnPropertyChanged(nameof(ShowPotaLookupText));
    }

    [ObservableProperty] private bool isUpdatingSotaDb;
    [ObservableProperty] private bool isUpdatingPotaDb;

    /// <summary>"Sota DB" sidebar button: downloads/rebuilds the local SOTA reference database (see
    /// SotaRefDatabase.UpdateAsync) so SOTA reference lookups on the entry form work offline and don't
    /// depend on any per-call network request.</summary>
    [RelayCommand]
    private async Task UpdateSotaDbAsync()
    {
        IsUpdatingSotaDb = true;
        try
        {
            int count = await _sotaRefDb.UpdateAsync();
            if (count > 0) _dialogService.ShowInfo($"SOTA reference database updated: {count:N0} summits.");
            else _dialogService.ShowError("Could not update the SOTA reference database. Check your connection and try again.");
        }
        finally
        {
            IsUpdatingSotaDb = false;
        }
    }

    /// <summary>"Pota DB" sidebar button, mirrors UpdateSotaDbAsync for PotaRefDatabase.</summary>
    [RelayCommand]
    private async Task UpdatePotaDbAsync()
    {
        IsUpdatingPotaDb = true;
        try
        {
            int count = await _potaRefDb.UpdateAsync();
            if (count > 0) _dialogService.ShowInfo($"POTA reference database updated: {count:N0} parks.");
            else _dialogService.ShowError("Could not update the POTA reference database. Check your connection and try again.");
        }
        finally
        {
            IsUpdatingPotaDb = false;
        }
    }

    // Contest/SKCC fields. These describe the *contacted* station (what they sent), so -- like SotaRef/
    // SigInfo above -- they reset per QSO, not sticky.
    [ObservableProperty] private string? skccNr;
    [ObservableProperty] private string? precedence;
    [ObservableProperty] private string? check;
    [ObservableProperty] private string? qsoClass;

    // MySkccNr describes the operator's own setup, same sticky rationale (and profile-seed/override
    // pattern) as MyGridSquare/MyState/MyCounty -- see SeedStationDefaults.
    [ObservableProperty] private string? mySkccNr;

    // QSL Via callsign (routing through a manager) -- genuinely per-QSO like SkccNr/Check/Class above,
    // no prior observable property or ShowXxxField gate at all (unlike the rest of this file's Stage 5
    // additions, which already had a gate and just needed XAML).
    [ObservableProperty] private string? qslViaCallsign;

    public bool ShowQslViaField => IsFieldVisible("QslVia");

    // SelectedPrecedenceOption drives the ComboBox (shows the full ARRL definition while choosing, see
    // ArrlPrecedenceOption); OnSelectedPrecedenceOptionChanged below keeps the plain Precedence string
    // (what actually gets saved to Qso.Precedence) in sync with it.
    [ObservableProperty] private ArrlPrecedenceOption? selectedPrecedenceOption;

    public ObservableCollection<ArrlPrecedenceOption> PrecedenceOptions { get; } = new(ArrlPrecedenceOptions.All);

    partial void OnSelectedPrecedenceOptionChanged(ArrlPrecedenceOption? value) => Precedence = value?.Value;

    // Preset field layout for the form below (Normal/Contest/SOTA/POTA) -- see QsoEntryModeFields for the
    // per-field visibility rules. Not persisted between visits: each contest/activation is a deliberate,
    // one-time choice at the start of a session, so the form always starts on Normal (everything shown).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDateTimeUtc))]
    [NotifyPropertyChangedFor(nameof(ShowTimeOff))]
    [NotifyPropertyChangedFor(nameof(ShowBand))]
    [NotifyPropertyChangedFor(nameof(ShowSubMode))]
    [NotifyPropertyChangedFor(nameof(ShowTxPower))]
    [NotifyPropertyChangedFor(nameof(ShowGridSquare))]
    [NotifyPropertyChangedFor(nameof(ShowState))]
    [NotifyPropertyChangedFor(nameof(ShowSotaFields))]
    [NotifyPropertyChangedFor(nameof(ShowPotaFields))]
    [NotifyPropertyChangedFor(nameof(ShowSkccFields))]
    [NotifyPropertyChangedFor(nameof(ShowContestExchangeFields))]
    [NotifyPropertyChangedFor(nameof(ShowCityCounty))]
    [NotifyPropertyChangedFor(nameof(ShowCountry))]
    [NotifyPropertyChangedFor(nameof(ShowArrlSection))]
    [NotifyPropertyChangedFor(nameof(ShowCqItuZone))]
    [NotifyPropertyChangedFor(nameof(ShowComment))]
    [NotifyPropertyChangedFor(nameof(ShowStationFields))]
    [NotifyPropertyChangedFor(nameof(ShowSequenceFields))]
    [NotifyPropertyChangedFor(nameof(ShowSkccField))]
    [NotifyPropertyChangedFor(nameof(ShowMySkccField))]
    [NotifyPropertyChangedFor(nameof(ShowPrecedenceField))]
    [NotifyPropertyChangedFor(nameof(ShowCheckField))]
    [NotifyPropertyChangedFor(nameof(ShowClassField))]
    [NotifyPropertyChangedFor(nameof(ShowTimeOffField))]
    [NotifyPropertyChangedFor(nameof(ShowGridField))]
    [NotifyPropertyChangedFor(nameof(ShowCityField))]
    [NotifyPropertyChangedFor(nameof(ShowStateField))]
    [NotifyPropertyChangedFor(nameof(ShowCountyField))]
    [NotifyPropertyChangedFor(nameof(ShowCountryField))]
    [NotifyPropertyChangedFor(nameof(ShowArrlSectionField))]
    [NotifyPropertyChangedFor(nameof(ShowCqZoneField))]
    [NotifyPropertyChangedFor(nameof(ShowItuZoneField))]
    [NotifyPropertyChangedFor(nameof(ShowMySotaField))]
    [NotifyPropertyChangedFor(nameof(ShowSotaField))]
    [NotifyPropertyChangedFor(nameof(ShowMyPotaField))]
    [NotifyPropertyChangedFor(nameof(ShowPotaField))]
    [NotifyPropertyChangedFor(nameof(ShowOpField))]
    [NotifyPropertyChangedFor(nameof(ShowQthField))]
    [NotifyPropertyChangedFor(nameof(ShowTxPowerField))]
    [NotifyPropertyChangedFor(nameof(ShowCommentField))]
    [NotifyPropertyChangedFor(nameof(ShowRow3))]
    [NotifyPropertyChangedFor(nameof(EntryFormTitle))]
    private QsoEntryModeOption selectedEntryModeOption = QsoEntryModeOptions.For(QsoEntryMode.Normal);

    public ObservableCollection<QsoEntryModeOption> EntryModeOptions { get; } = new(QsoEntryModeOptions.All);

    /// <summary>IsFieldVisible now reads a per-mode hidden-columns set (SettingsService.GetHiddenColumns),
    /// so switching mode changes which optional fields show up even though no column was actually
    /// toggled -- NotifyFieldVisibilityChanged re-evaluates every IsFieldVisible-driven *Field property to
    /// pick that up (the attributes above SelectedEntryModeOption only cover the mode-category gates,
    /// e.g. ShowSotaFields, not the leaf column-gated properties).</summary>
    partial void OnSelectedEntryModeOptionChanged(QsoEntryModeOption value) => NotifyFieldVisibilityChanged();

    /// <summary>Entry form's header text, e.g. "Entry Form - Net Control Mode (...)" -- reflects whatever
    /// display name the operator gave this mode in the Column Visibility picker's Rename Tab feature (see
    /// SettingsService.GetModeTabLabel), falling back to the mode's own name. Refreshes on mode switch (see
    /// the attribute above) and on rename (see NotifyModeLabelsChanged, bridged from QsoLogViewModel.
    /// ModeLabelsChanged via MainViewModel, same pattern as NotifyFieldVisibilityChanged).</summary>
    public string EntryFormTitle =>
        $"Entry Form - {_settings.GetModeTabLabel(SelectedEntryModeOption.Value.ToString(), DefaultModeLabel(SelectedEntryModeOption.Value))} Mode (Fields are movable within tabs)";

    private static string DefaultModeLabel(QsoEntryMode mode) => mode switch
    {
        QsoEntryMode.Sota => "SOTA",
        QsoEntryMode.Pota => "POTA",
        _ => mode.ToString(),
    };

    public void NotifyModeLabelsChanged() => OnPropertyChanged(nameof(EntryFormTitle));

    public bool ShowDateTimeUtc => QsoEntryModeFields.ShowDateTimeUtc(SelectedEntryModeOption.Value);
    public bool ShowTimeOff => QsoEntryModeFields.ShowTimeOff(SelectedEntryModeOption.Value);
    public bool ShowBand => QsoEntryModeFields.ShowBand(SelectedEntryModeOption.Value);
    public bool ShowSubMode => QsoEntryModeFields.ShowSubMode(SelectedEntryModeOption.Value);
    public bool ShowTxPower => QsoEntryModeFields.ShowTxPower(SelectedEntryModeOption.Value);
    public bool ShowGridSquare => QsoEntryModeFields.ShowGridSquare(SelectedEntryModeOption.Value);
    public bool ShowState => QsoEntryModeFields.ShowState(SelectedEntryModeOption.Value);
    public bool ShowSotaFields => QsoEntryModeFields.ShowSotaFields(SelectedEntryModeOption.Value);
    public bool ShowPotaFields => QsoEntryModeFields.ShowPotaFields(SelectedEntryModeOption.Value);
    public bool ShowSkccFields => QsoEntryModeFields.ShowSkccFields(SelectedEntryModeOption.Value);
    public bool ShowContestExchangeFields => QsoEntryModeFields.ShowContestExchangeFields(SelectedEntryModeOption.Value);
    public bool ShowCityCounty => QsoEntryModeFields.ShowCityCounty(SelectedEntryModeOption.Value);
    public bool ShowCountry => QsoEntryModeFields.ShowCountry(SelectedEntryModeOption.Value);
    public bool ShowArrlSection => QsoEntryModeFields.ShowArrlSection(SelectedEntryModeOption.Value);
    public bool ShowCqItuZone => QsoEntryModeFields.ShowCqItuZone(SelectedEntryModeOption.Value);
    public bool ShowComment => QsoEntryModeFields.ShowComment(SelectedEntryModeOption.Value);

    // Row 3 should be visible in any mode except Normal
    public bool ShowRow3 => SelectedEntryModeOption.Value != QsoEntryMode.Normal;

    // Op/Qth aren't part of any preset's requested field list (unlike CvarcCellLog, this form has no
    // separate read-only "Your Station" section -- Op/Qth are editable fields right here), so they're
    // Normal-only like Comment/City/County/Country above. WPF-specific (CvarcCellLog never toggles these),
    // so this isn't in the shared QsoEntryModeFields table.
    public bool ShowStationFields => SelectedEntryModeOption.Value == QsoEntryMode.Normal;

    // Contest-only, matching CvarcCellLog's identical Sequence # feature (see SequenceNumber below).
    public bool ShowSequenceFields => SelectedEntryModeOption.Value is QsoEntryMode.Contest or QsoEntryMode.All;

    // Every optional entry-form field's visibility is decided *solely* by the per-mode Columns picker
    // checkbox (IsFieldVisible, see SettingsService.GetHiddenColumns) -- these properties used to also
    // AND in one of the QsoEntryModeFields.ShowXxx/ShowXxxFields category gates above (a fixed preset of
    // which fields exist per mode, predating the drag-and-drop customizable-per-mode feature), but that
    // meant checking a field on in, say, Contest's picker tab did nothing for fields the old Normal-only
    // preset never allowed there -- e.g. Comment (ShowComment was Normal/All only), TimeOff, Grid, City,
    // County, Op, Qth, and others. The whole point of the per-mode picker is that the operator decides
    // per mode now, so the hardcoded category gate can only ever take away a choice the picker already
    // made, never grant one. QsoEntryModeFields itself is untouched (still used by CvarcCellLog, which
    // doesn't have this per-mode customization feature) -- only these *Field leaf properties stopped
    // consulting it.
    public bool ShowSkccField => IsFieldVisible("Skcc");
    public bool ShowMySkccField => IsFieldVisible("MySkcc");
    public bool ShowPrecedenceField => IsFieldVisible("Precedence");
    public bool ShowCheckField => IsFieldVisible("Check");
    public bool ShowClassField => IsFieldVisible("Class");
    public bool ShowTimeOffField => IsFieldVisible("TimeOff");
    public bool ShowGridField => IsFieldVisible("Grid");
    public bool ShowCityField => IsFieldVisible("City");
    public bool ShowStateField => IsFieldVisible("State");
    public bool ShowCountyField => IsFieldVisible("County");
    public bool ShowCountryField => IsFieldVisible("Country");
    public bool ShowArrlSectionField => IsFieldVisible("ArrlSection");
    public bool ShowCqZoneField => IsFieldVisible("CqZone");
    public bool ShowItuZoneField => IsFieldVisible("ItuZone");
    public bool ShowMySotaField => IsFieldVisible("MySota");
    public bool ShowSotaField => IsFieldVisible("Sota");
    public bool ShowMyPotaField => IsFieldVisible("MyPota");
    public bool ShowPotaField => IsFieldVisible("Pota");
    public bool ShowOpField => IsFieldVisible("Op");
    public bool ShowQthField => IsFieldVisible("Qth");
    public bool ShowTxPowerField => IsFieldVisible("TxPower");
    public bool ShowCommentField => IsFieldVisible("Comment");
    public bool ShowFreqRxField => IsFieldVisible("FreqRx");
    public bool ShowQslField => IsFieldVisible("Qsl");
    public bool ShowLotwField => IsFieldVisible("Lotw");
    public bool ShowMyCountyField => IsFieldVisible("MyCounty");
    public bool ShowContinentField => IsFieldVisible("Continent");
    public bool ShowMyGridField => IsFieldVisible("MyGrid");
    public bool ShowMyStateField => IsFieldVisible("MyState");
    public bool ShowSequenceField => IsFieldVisible("Sequence");
    public bool ShowModeField => IsFieldVisible("Mode");
    public bool ShowSubModeField => IsFieldVisible("SubMode");
    public bool ShowUtcTimeField => IsFieldVisible("UtcTime");
    public bool ShowStartTimeField => IsFieldVisible("UtcTime");
    public bool ShowEndTimeField => IsFieldVisible("UtcTime");
    public bool ShowNameField => IsFieldVisible("Name");
    public bool ShowLocalTimeField => IsFieldVisible("LocalTime");
    public bool ShowBandField => IsFieldVisible("Band");
    public bool ShowFreqField => IsFieldVisible("Freq");
    public bool ShowRstField => IsFieldVisible("Rst");

    // Contest-style running sequence number, saved into Qso.StxSerial (reserved for exactly this since
    // the contest logging work, never wired to a UI until now). Sticky across QSOs (not cleared in
    // ResetForNextQso) -- Start sets it to 1 and arms auto-increment; each successful Log QSO afterward
    // bumps it by 1 so the next contact gets the next number. Reset zeroes it and disarms auto-increment
    // until Start is pressed again, so StxSerial goes back to being omitted (null) from saved QSOs.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SequenceNumberText))]
    private int sequenceNumber;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SequenceNumberText))]
    private bool isSequenceActive;

    public string SequenceNumberText => IsSequenceActive ? SequenceNumber.ToString(CultureInfo.InvariantCulture) : "—";

    [RelayCommand]
    private void StartSequence()
    {
        SequenceNumber = 1;
        IsSequenceActive = true;
    }

    [RelayCommand]
    private void ResetSequence()
    {
        SequenceNumber = 0;
        IsSequenceActive = false;
    }

    // Op/Qth describe the operator's own setup, so -- like Band/Mode/RstSent/RstRcvd -- they're sticky
    // rather than reset in ResetForNextQso; TxPowerWatts is the same, since a station typically runs at
    // one consistent power for a whole operating session rather than changing it contact to contact.
    [ObservableProperty] private string? op;
    [ObservableProperty] private string? qth;
    [ObservableProperty] private string? txPowerWatts;

    // MyGridSquare/MyState/MyCounty describe the operator's own location, same sticky rationale as Qth/Op
    // above -- seeded from the station profile (see SeedStationDefaults) but editable per-session (e.g.
    // a portable operation temporarily away from the profile's usual location), not reset in
    // ResetForNextQso, and only re-seeded when the station profile selection changes.
    [ObservableProperty] private string? myGridSquare;
    [ObservableProperty] private string? myState;
    [ObservableProperty] private string? myCounty;

    // FrequencyRxMhz (split operation) and Continent/QslSent/QslRcvd/LotwQslSent/LotwQslRcvd are
    // genuinely per-QSO, so unlike the sticky group above they do get cleared in ResetForNextQso.
    // Continent is also auto-filled from the callsign lookup at save time if left blank (see LogQsoAsync),
    // same fallback pattern QsoEditViewModel already uses for a previously-logged QSO.
    [ObservableProperty] private string? frequencyRxMhz;
    [ObservableProperty] private string? continent;
    [ObservableProperty] private QslStatus qslSent;
    [ObservableProperty] private QslStatus qslRcvd;
    [ObservableProperty] private QslStatus lotwQslSent;
    [ObservableProperty] private QslStatus lotwQslRcvd;

    /// <summary>QSL/LoTW status ComboBox choices, same enum-array pattern as QsoEditViewModel.QslStatuses.</summary>
    public Array QslStatuses { get; } = Enum.GetValues(typeof(QslStatus));
    [ObservableProperty] private string? comment;
    [ObservableProperty] private bool isLookingUp;
    [ObservableProperty] private StationProfile? selectedStationProfile;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CatStatusText))]
    [NotifyPropertyChangedFor(nameof(CatIndicatorColor))]
    [NotifyPropertyChangedFor(nameof(CatConnectButtonLabel))]
    private bool isCatConnected;
    [ObservableProperty] private bool isCatAutoFillPaused;
    [ObservableProperty] private string? catStatusMessage;

    // Drive the middle bar's CAT status indicator/button (see MainWindow.xaml) off the real
    // IsCatConnected state set by ToggleCatConnectionAsync -- both were previously hardcoded and never
    // reflected an actual connection either way.
    public string CatStatusText => IsCatConnected ? "CAT: Connected" : "CAT: Disconnected";
    public string CatIndicatorColor => IsCatConnected ? "#2ECC71" : "#999999";
    public string CatConnectButtonLabel => IsCatConnected ? "📡 Disconnect CAT" : "📡 Connect CAT";
    [ObservableProperty] private string? subMode;

    public ObservableCollection<string> Bands { get; } = new(QsoFieldOptions.Bands);
    public ObservableCollection<string> Modes { get; } = new(QsoFieldOptions.Modes);

    /// <summary>Sub-Mode picker contents — swapped between QsoFieldOptions.DataSubModes and
    /// .SsbSubModes as Mode changes, see OnModeChanged. Starts as SsbSubModes to match the "SSB"
    /// default above.</summary>
    public ObservableCollection<string> SubModes { get; } = new(QsoFieldOptions.SsbSubModes);

    public ObservableCollection<StationProfile> StationProfiles { get; } = new();

    public QsoEntryViewModel(
        IQsoRepository qsoRepository,
        IStationProfileRepository stationProfileRepository,
        ICallsignEntityResolver entityResolver,
        IGridZoneResolver gridZoneResolver,
        LookupCoordinator lookupCoordinator,
        RigControlCoordinator rigCoordinator,
        InternetCatCoordinator internetCat,
        SettingsService settings,
        DialogService dialogService,
        IClock clock,
        GridTrackerBroadcastService gridTrackerBroadcast,
        SotaRefDatabase sotaRefDb,
        PotaRefDatabase potaRefDb)
    {
        _qsoRepository = qsoRepository;
        _stationProfileRepository = stationProfileRepository;
        _entityResolver = entityResolver;
        _gridZoneResolver = gridZoneResolver;
        _lookupCoordinator = lookupCoordinator;
        _rigCoordinator = rigCoordinator;
        _internetCat = internetCat;
        _settings = settings;
        _dialogService = dialogService;
        _clock = clock;
        _gridTrackerBroadcast = gridTrackerBroadcast;
        _sotaRefDb = sotaRefDb;
        _potaRefDb = potaRefDb;

        bandIsStatic = _settings.IsFieldStatic("Band");
        freqIsStatic = _settings.IsFieldStatic("Freq");
        modeIsStatic = _settings.IsFieldStatic("Mode");
        subModeIsStatic = _settings.IsFieldStatic("SubMode");
        rstSentIsStatic = _settings.IsFieldStatic("RstSent");
        rstRcvdIsStatic = _settings.IsFieldStatic("RstRcvd");
        opIsStatic = _settings.IsFieldStatic("Op");
        txPowerIsStatic = _settings.IsFieldStatic("TxPower");
        myGridIsStatic = _settings.IsFieldStatic("MyGrid");
        myStateIsStatic = _settings.IsFieldStatic("MyState");
        mySotaIsStatic = _settings.IsFieldStatic("MySota");
        myPotaIsStatic = _settings.IsFieldStatic("MyPota");

        _catPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _catPollTimer.Tick += OnCatPollTick;

        _liveClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveClockTimer.Tick += OnLiveClockTick;
        _liveClockTimer.Start();
    }

    /// <summary>Keeps QsoDateTimeUtcText (and, via the existing sync, QsoDateTimeLocalText) advancing to
    /// the actual current time while the entry form sits idle -- runs continuously rather than only at
    /// InitializeAsync/ResetForNextQso, matching how ham radio loggers conventionally show a live clock.
    /// Stops touching the UTC field the moment the operator types a manual/backdated time or presses
    /// "Start Time" (see _dateTimeManuallyEdited) so a deliberate edit/freeze is never clobbered. Once UTC
    /// is frozen, Local Time switches to ticking independently (its own _localTimeManuallyEdited guard)
    /// instead of riding UTC's sync, so "Start Time" freezes only the UTC field as intended.</summary>
    private void OnLiveClockTick(object? sender, EventArgs e)
    {
        if (!_dateTimeManuallyEdited)
        {
            _isLiveClockUpdate = true;
            try
            {
                QsoDateTimeUtcText = _clock.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            }
            finally
            {
                _isLiveClockUpdate = false;
            }
            return;
        }

        if (_localTimeManuallyEdited) return;

        _isLiveClockUpdate = true;
        try
        {
            QsoDateTimeLocalText = _clock.UtcNow.AddHours(CurrentUtcOffsetHours).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isLiveClockUpdate = false;
        }
    }

    /// <summary>"Start Time" button next to Date/Time (UTC): freezes that field at the current instant by
    /// setting _dateTimeManuallyEdited, same as typing a time by hand -- can't just re-stamp the value via
    /// OnLiveClockTick's own side effect, since the live clock already keeps this field showing "now" to
    /// the second, so the string this produces is often identical to what's already displayed, and the
    /// generated property setter skips the change notification (and thus the partial method) entirely
    /// when the value doesn't change. Relying on that side effect meant the freeze silently failed most of
    /// the time.</summary>
    [RelayCommand]
    private void SetStartTime()
    {
        _dateTimeManuallyEdited = true;
        QsoDateTimeUtcText = _clock.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>"End Time" button next to Time Off (UTC): records the current instant into that field,
    /// one shot -- unlike Start Time, there's no live clock on this field to freeze.</summary>
    [RelayCommand]
    private void SetEndTime() =>
        QsoDateTimeOffUtcText = _clock.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>Lets MainViewModel drive the log grid's search filter from this field, so the operator
    /// sees prior contacts with a station (duplicate check, history) as they type — mirrors how
    /// QsoLogged already drives QsoLog.RefreshAsync from here.</summary>
    partial void OnCallsignChanged(string value) => CallsignChanged?.Invoke(this, value);

    /// <summary>Hours to add to a UTC time to get the selected station's local time (DST-adjusted) --
    /// same basis as Qso.LocalDateTimeOn, just computed ahead of save time so the entry form can show
    /// both live as the operator types either one.</summary>
    private double CurrentUtcOffsetHours =>
        (double)(SelectedStationProfile?.UtcOffsetHours ?? 0m) + (SelectedStationProfile?.ObservesDaylightSavingTime == true ? 1 : 0);

    /// <summary>Typing a UTC date/time recomputes the Local Time field to match, so an operator entering
    /// a QSO after the fact can type whichever one they know and see the other filled in automatically.</summary>
    partial void OnQsoDateTimeUtcTextChanged(string value)
    {
        if (_isSyncingDateTime) return;
        if (!_isLiveClockUpdate) _dateTimeManuallyEdited = true;
        if (!TryParseQsoDateTime(value, out var utc)) return;

        _isSyncingDateTime = true;
        try
        {
            QsoDateTimeLocalText = utc.AddHours(CurrentUtcOffsetHours).ToString(SyncFormatFor(value), CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
    }

    /// <summary>Mirror of OnQsoDateTimeUtcTextChanged for the other direction -- typing Local Time
    /// recomputes UTC. Skipped entirely for OnLiveClockTick's own post-freeze Local writes (see
    /// _isLiveClockUpdate there) -- those must not re-derive and overwrite the now-frozen UTC field.</summary>
    partial void OnQsoDateTimeLocalTextChanged(string value)
    {
        if (_isLiveClockUpdate) return;
        if (_isSyncingDateTime) return;
        _dateTimeManuallyEdited = true;
        _localTimeManuallyEdited = true;
        if (!TryParseQsoDateTime(value, out var local)) return;

        _isSyncingDateTime = true;
        try
        {
            // Always the short form here: this handler only ever runs for a genuine manual edit of the
            // Local field (sync-driven writes are caught by the _isSyncingDateTime guard above, and the
            // live clock's own writes by the _isLiveClockUpdate guard above).
            QsoDateTimeUtcText = local.AddHours(-CurrentUtcOffsetHours).ToString(ShortDateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
    }

    /// <summary>Switching stations changes the UTC-offset basis -- re-derive Local Time from the
    /// still-authoritative UTC text rather than leaving it showing the previous station's local time.
    /// Also reseeds Op/Qth to the newly-selected station's own defaults (see SeedStationDefaults).</summary>
    partial void OnSelectedStationProfileChanged(StationProfile? value)
    {
        SeedStationDefaults(value);

        if (!TryParseQsoDateTime(QsoDateTimeUtcText, out var utc)) return;

        _isSyncingDateTime = true;
        try
        {
            QsoDateTimeLocalText = utc.AddHours(CurrentUtcOffsetHours).ToString(SyncFormatFor(QsoDateTimeUtcText), CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
    }

    /// <summary>Seeds Op/Qth from the station profile's own defaults -- Operator Callsign takes
    /// priority over the profile's free-text Op name when both are set, same rule LogQsoAsync used to
    /// apply directly at save time before these became editable entry-form fields. The operator can
    /// still override either field afterward for this specific QSO.</summary>
    private void SeedStationDefaults(StationProfile? profile)
    {
        Qth = profile?.Qth;
        Op = !string.IsNullOrWhiteSpace(profile?.OperatorCallsign) ? profile.OperatorCallsign : profile?.Op;
        MyGridSquare = profile?.MyGridSquare;
        MyState = profile?.MyState;
        MyCounty = profile?.MyCounty;
        MySkccNr = profile?.SkccNr;
    }

    /// <summary>Swaps the Sub-Mode picker's contents to match the newly-selected Mode, and clears any
    /// leftover selection that doesn't belong to the new list — so a PSK sub-mode can't silently
    /// persist into an SSB QSO or vice versa, and neither persists into CW/FM/etc, which show no
    /// Sub-Mode picker at all (see SubModeVisibilityConverter).</summary>
    partial void OnModeChanged(string value)
    {
        IReadOnlyList<string> options =
            string.Equals(value, "PSK", StringComparison.OrdinalIgnoreCase) ? QsoFieldOptions.PskSubModes
            : string.Equals(value, "DIGITALVOICE", StringComparison.OrdinalIgnoreCase) ? QsoFieldOptions.DigitalVoiceSubModes
            : string.Equals(value, "SSB", StringComparison.OrdinalIgnoreCase) ? QsoFieldOptions.SsbSubModes
            : Array.Empty<string>();

        if (SubMode is not null && !options.Contains(SubMode, StringComparer.OrdinalIgnoreCase)) SubMode = null;
        SubModes.Clear();
        foreach (var option in options) SubModes.Add(option);
    }

    public async Task InitializeAsync()
    {
        var profiles = await _stationProfileRepository.GetAllAsync();
        StationProfiles.Clear();
        foreach (var p in profiles) StationProfiles.Add(p);

        SelectedStationProfile = StationProfiles.FirstOrDefault(p => p.Id == _settings.LastUsedStationProfileId)
            ?? StationProfiles.FirstOrDefault(p => p.IsDefault)
            ?? StationProfiles.FirstOrDefault();

        _dateTimeManuallyEdited = false;
        _localTimeManuallyEdited = false;
        _isLiveClockUpdate = true;
        try
        {
            QsoDateTimeUtcText = _clock.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isLiveClockUpdate = false;
        }
    }

    [RelayCommand]
    private async Task LookupAsync()
    {
        if (string.IsNullOrWhiteSpace(Callsign)) return;
        IsLookingUp = true;
        try
        {
            await PerformLookupAsync(showErrorDialog: true);
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    /// <summary>Runs the online lookup and fills in currently-empty fields, tracking which callsign
    /// was looked up so LogQsoAsync can tell whether it still needs to run one. Never throws — lookup
    /// failures are best-effort and, outside the manual Lookup button, silent (a lookup miss must never
    /// block logging a QSO).</summary>
    private async Task PerformLookupAsync(bool showErrorDialog)
    {
        string normalizedCallsign = Callsign.Trim().ToUpperInvariant();
        var result = await _lookupCoordinator.LookupAsync(normalizedCallsign);
        _lastLookedUpCallsign = normalizedCallsign;

        if (result.Found)
        {
            Name = result.Name ?? Name;
            GridSquare = result.GridSquare ?? GridSquare;
            Country = result.Country ?? Country;
            State = result.State ?? State;
            County = result.County ?? County;
            City = result.City ?? City;
        }
        else if (showErrorDialog && !string.IsNullOrEmpty(result.Error))
        {
            _dialogService.ShowError($"Lookup failed: {result.Error}");
        }

        // ARRL Section is derived from State/County rather than looked up directly -- see
        // ArrlSectionResolver for the state/county table and its accuracy caveats.
        ArrlSection = ArrlSectionResolver.Resolve(State, County) ?? ArrlSection;

        // CQ/ITU zone: prefer resolving from the grid square (accurate down to the ~1deg band the
        // station is actually in — matters a lot for split-zone countries like the USA, which spans
        // CQ zones 3-8 depending on where in the country a station is). Fall back to the DXCC entity's
        // nominal zone (a single value per country) only when there's no usable grid square to work
        // from. Neither depends on the online lookup service finding a match, so both run regardless.
        var resolvedEntity = await _entityResolver.ResolveAsync(normalizedCallsign);
        var (gridCqZone, gridItuZone) = _gridZoneResolver.Resolve(GridSquare);
        CqZone = gridCqZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.CqZone?.ToString(CultureInfo.InvariantCulture) ?? CqZone;
        ItuZone = gridItuZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.ItuZone?.ToString(CultureInfo.InvariantCulture) ?? ItuZone;
    }

    [RelayCommand]
    private async Task ToggleCatConnectionAsync()
    {
        if (IsCatConnected)
        {
            _catPollTimer.Stop();
            if (_connectedViaInternet) await _internetCat.DisconnectAsync();
            else await _rigCoordinator.DisconnectAsync();
            IsCatConnected = false;
            CatStatusMessage = "CAT disconnected.";
            return;
        }

        // Internet Control (network K4), when enabled in Settings, takes precedence over the Hamlib serial
        // path. _connectedViaInternet is latched here so Disconnect/Poll keep using this same backend even
        // if the enable setting is flipped while connected.
        if (_settings.InternetRadioEnabled)
        {
            var (ok, err) = await _internetCat.ConnectAsync();
            IsCatConnected = ok;
            _connectedViaInternet = ok;
            CatStatusMessage = ok ? "Internet CAT connected." : $"Internet CAT connect failed: {err}";
            if (ok) _catPollTimer.Start();
            return;
        }

        var (success, error) = await _rigCoordinator.ConnectAsync();
        IsCatConnected = success;
        _connectedViaInternet = false;
        CatStatusMessage = success ? "CAT connected." : $"CAT connect failed: {error}";
        if (success) _catPollTimer.Start();
    }

    [RelayCommand]
    private void ToggleCatAutoFillPause()
    {
        IsCatAutoFillPaused = !IsCatAutoFillPaused;
    }

    [RelayCommand]
    private void CycleLogMode()
    {
        var allModes = EntryModeOptions.ToList();
        var currentIndex = allModes.FindIndex(m => m.Value == SelectedEntryModeOption.Value);
        var nextIndex = (currentIndex + 1) % allModes.Count;
        SelectedEntryModeOption = allModes[nextIndex];
    }

    private async void OnCatPollTick(object? sender, EventArgs e)
    {
        // Frequency/Band/Mode/TX Power are the only fields CAT ever writes — they should keep tracking
        // the live radio state even while the operator is mid-way typing the next contact's callsign.
        // "Pause auto-fill" is the deliberate, explicit control for stopping that.
        if (IsCatAutoFillPaused) return;

        if (_connectedViaInternet) await PollInternetCatAsync();
        else await PollRigCatAsync();
    }

    private async Task PollRigCatAsync()
    {
        var result = await _rigCoordinator.PollAsync();
        if (!result.Success)
        {
            CatStatusMessage = $"CAT: {result.Error}";
            if (_rigCoordinator.State != RigConnectionState.Connected)
            {
                Log.Warning("CAT poll failed and rig is no longer connected — stopping poll timer: {Error}", result.Error);
                _catPollTimer.Stop();
                IsCatConnected = false;
            }
            return;
        }

        FrequencyMhz = result.FrequencyMhz?.ToString("0.000000");
        if (result.Band is not null) Band = result.Band;
        Mode = result.MappedMode ?? Mode;
        if (result.SubMode is not null) SubMode = result.SubMode;

        // RFPOWER is a fraction (0.0-1.0) of the active radio's own configured max wattage, not real
        // watts -- see RadioProfile.MaxPowerWatts. Leave TxPowerWatts alone (it's sticky between QSOs,
        // like Op/Qth) if this rig doesn't report RFPOWER or no max wattage is configured for it.
        if (result.PowerFraction is decimal fraction && _rigCoordinator.ActiveRadioMaxPowerWatts is int maxWatts)
        {
            TxPowerWatts = Math.Round(fraction * maxWatts, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        }

        CatStatusMessage = null;
    }

    private async Task PollInternetCatAsync()
    {
        var result = await _internetCat.PollAsync();
        if (!result.Success)
        {
            CatStatusMessage = $"CAT: {result.Error}";
            if (_internetCat.State != K4ConnectionState.Connected)
            {
                Log.Warning("Internet CAT poll failed and radio is no longer connected — stopping poll timer: {Error}", result.Error);
                _catPollTimer.Stop();
                IsCatConnected = false;
            }
            return;
        }

        FrequencyMhz = result.FrequencyMhz?.ToString("0.000000");
        if (result.Band is not null) Band = result.Band;
        Mode = result.MappedMode ?? Mode;
        if (result.SubMode is not null) SubMode = result.SubMode;

        // Unlike Hamlib's RFPOWER fraction, the K4's PCX; reply is already actual watts (see
        // K4ReplyParser), so it maps straight to TX Power with no max-wattage scaling. Left sticky
        // between QSOs when the radio doesn't report a parseable power.
        if (result.PowerWatts is decimal watts)
        {
            TxPowerWatts = Math.Round(watts, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        }

        CatStatusMessage = null;
    }

    [RelayCommand]
    private async Task LogQsoAsync()
    {
        if (string.IsNullOrWhiteSpace(Callsign))
        {
            _dialogService.ShowError("Enter a callsign before logging the QSO.");
            return;
        }
        if (SelectedStationProfile is null)
        {
            _dialogService.ShowError("Select a station profile before logging the QSO (add one under Stations first).");
            return;
        }
        if (!TryParseQsoDateTime(QsoDateTimeUtcText, out var qsoDateTimeUtc))
        {
            _dialogService.ShowError("Date/Time (UTC) must be in the format yyyy-MM-dd HH:mm:ss or yyyy-MM-dd HH:mm.");
            return;
        }

        // Make sure the online lookup has run for this callsign before saving — covers both "never
        // clicked Lookup" and "clicked Lookup, then edited the callsign afterward."
        if (!string.Equals(_lastLookedUpCallsign, Callsign.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            IsLookingUp = true;
            try
            {
                await PerformLookupAsync(showErrorDialog: false);
            }
            finally
            {
                IsLookingUp = false;
            }
        }

        var qso = new Qso
        {
            Callsign = Callsign.Trim().ToUpperInvariant(),
            QsoDateTimeOnUtc = DateTime.SpecifyKind(qsoDateTimeUtc, DateTimeKind.Utc),
            QsoDateTimeOffUtc = !string.IsNullOrWhiteSpace(QsoDateTimeOffUtcText) && TryParseQsoDateTime(QsoDateTimeOffUtcText, out var qsoDateTimeOffUtc)
                ? DateTime.SpecifyKind(qsoDateTimeOffUtc, DateTimeKind.Utc)
                : null,
            Band = Band,
            Mode = Mode,
            SubMode = string.Equals(Mode, "DATA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Mode, "SSB", StringComparison.OrdinalIgnoreCase) ? SubMode : null,
            FrequencyMhz = decimal.TryParse(FrequencyMhz, out var freq) ? freq : null,
            RstSent = RstSent,
            RstRcvd = RstRcvd,
            Name = Name,
            GridSquare = GridSquare,
            City = City,
            State = State?.Trim().ToUpperInvariant(),
            County = County,
            Country = Country,
            ArrlSection = ArrlSection?.Trim().ToUpperInvariant(),
            CqZone = int.TryParse(CqZone, out var cqZoneValue) ? cqZoneValue : null,
            ItuZone = int.TryParse(ItuZone, out var ituZoneValue) ? ituZoneValue : null,
            MySotaRef = MySotaRef,
            SotaRef = SotaRef,
            MySigInfo = MySigInfo,
            SigInfo = SigInfo,
            SkccNr = SkccNr,
            Precedence = Precedence,
            Check = Check,
            Class = QsoClass,
            StxSerial = IsSequenceActive ? SequenceNumber : null,
            TxPowerWatts = decimal.TryParse(TxPowerWatts, out var txPower) ? txPower : null,
            Comment = Comment,
            StationProfileId = SelectedStationProfile.Id,
            StationCallsign = SelectedStationProfile.Callsign,
            OperatorCallsign = SelectedStationProfile.OperatorCallsign,
            // Qth/Op/MyGridSquare/MyState/MyCounty/MySkccNr default from the station profile (see
            // SeedStationDefaults) but are editable on the entry form, so this QSO gets whatever the
            // operator left them as, not necessarily the profile's own value.
            Qth = Qth,
            Op = Op,
            MyGridSquare = MyGridSquare,
            MyState = MyState,
            MyCounty = MyCounty,
            MySkccNr = MySkccNr,
            QslViaCallsign = QslViaCallsign,
            FrequencyRxMhz = decimal.TryParse(FrequencyRxMhz, NumberStyles.Number, CultureInfo.InvariantCulture, out var freqRx) ? freqRx : null,
            Continent = Continent,
            QslSent = QslSent,
            QslRcvd = QslRcvd,
            LotwQslSent = LotwQslSent,
            LotwQslRcvd = LotwQslRcvd,
            UtcOffsetHours = SelectedStationProfile.UtcOffsetHours,
            ObservesDaylightSavingTime = SelectedStationProfile.ObservesDaylightSavingTime,
        };

        var resolvedEntity = await _entityResolver.ResolveAsync(qso.Callsign);
        if (resolvedEntity is not null)
        {
            qso.DxccEntityCode = resolvedEntity.EntityCode;
            qso.Country = string.IsNullOrWhiteSpace(qso.Country) ? resolvedEntity.EntityName : qso.Country;
            qso.Continent ??= resolvedEntity.Continent;
            qso.CqZone ??= resolvedEntity.CqZone;
            qso.ItuZone ??= resolvedEntity.ItuZone;
        }

        await _qsoRepository.AddAsync(qso);
        _gridTrackerBroadcast.BroadcastQso(qso);

        _settings.LastUsedStationProfileId = SelectedStationProfile.Id;

        QsoLogged?.Invoke(this, EventArgs.Empty);
        ResetForNextQso();
    }

    /// <summary>Wipes every QSO from the current log after an explicit Yes/No confirmation. Destructive
    /// and irreversible, so it's gated on the confirm and reuses the QsoLogged event to refresh the (now
    /// empty) grid.</summary>
    [RelayCommand]
    private async Task ClearDatabaseAsync()
    {
        if (!_dialogService.Confirm(
                "This permanently deletes ALL QSOs from the current log." + Environment.NewLine +
                "This cannot be undone." + Environment.NewLine + Environment.NewLine +
                "Clear the entire log?"))
            return;

        int removed = await _qsoRepository.DeleteAllAsync();
        QsoLogged?.Invoke(this, EventArgs.Empty);
        _dialogService.ShowInfo($"Cleared the log. {removed} QSO(s) deleted.");
    }

    // Per-field "static" (sticky-across-QSOs) toggle -- a checkbox next to each field's label in
    // QsoEntryView.xaml, replacing what used to be a fixed "(static)" label. Backed by
    // SettingsService.IsFieldStatic/SetFieldStatic (global, not per-mode: it's a workflow habit, not a
    // display choice). Initial values are seeded from settings in the constructor by assigning the
    // backing fields directly, not the properties, so that seeding doesn't immediately re-save the same
    // value it just read. Station isn't included here: it's the active station-profile selection, not
    // per-QSO data, so "not static" has no coherent meaning for it -- its label stays plain text.
    [ObservableProperty] private bool bandIsStatic;
    [ObservableProperty] private bool freqIsStatic;
    [ObservableProperty] private bool modeIsStatic;
    [ObservableProperty] private bool subModeIsStatic;
    [ObservableProperty] private bool rstSentIsStatic;
    [ObservableProperty] private bool rstRcvdIsStatic;
    [ObservableProperty] private bool opIsStatic;
    [ObservableProperty] private bool txPowerIsStatic;
    [ObservableProperty] private bool myGridIsStatic;
    [ObservableProperty] private bool myStateIsStatic;
    [ObservableProperty] private bool mySotaIsStatic;
    [ObservableProperty] private bool myPotaIsStatic;

    partial void OnBandIsStaticChanged(bool value) => _settings.SetFieldStatic("Band", value);
    partial void OnFreqIsStaticChanged(bool value) => _settings.SetFieldStatic("Freq", value);
    partial void OnModeIsStaticChanged(bool value) => _settings.SetFieldStatic("Mode", value);
    partial void OnSubModeIsStaticChanged(bool value) => _settings.SetFieldStatic("SubMode", value);
    partial void OnRstSentIsStaticChanged(bool value) => _settings.SetFieldStatic("RstSent", value);
    partial void OnRstRcvdIsStaticChanged(bool value) => _settings.SetFieldStatic("RstRcvd", value);
    partial void OnOpIsStaticChanged(bool value) => _settings.SetFieldStatic("Op", value);
    partial void OnTxPowerIsStaticChanged(bool value) => _settings.SetFieldStatic("TxPower", value);
    partial void OnMyGridIsStaticChanged(bool value) => _settings.SetFieldStatic("MyGrid", value);
    partial void OnMyStateIsStaticChanged(bool value) => _settings.SetFieldStatic("MyState", value);
    partial void OnMySotaIsStaticChanged(bool value) => _settings.SetFieldStatic("MySota", value);
    partial void OnMyPotaIsStaticChanged(bool value) => _settings.SetFieldStatic("MyPota", value);

    private void ResetForNextQso()
    {
        _lastLookedUpCallsign = null;
        Callsign = string.Empty;
        if (IsSequenceActive) SequenceNumber++;

        _dateTimeManuallyEdited = false;
        _localTimeManuallyEdited = false;
        _isLiveClockUpdate = true;
        try
        {
            QsoDateTimeUtcText = _clock.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isLiveClockUpdate = false;
        }

        QsoDateTimeOffUtcText = null;
        // Each of these carries over to the next QSO by default (like Band and Mode) so a manually-tuned
        // value doesn't have to be re-typed for every contact -- most are the operator's own choice per
        // field now (the checkbox next to each field's label in QsoEntryView.xaml), conditional on their
        // own IsStatic flag instead of simply never being touched here. Qth/MyCounty/MySkccNr went back
        // to being unconditionally sticky (no checkbox) -- too many fields got the toggle at first pass.
        if (!BandIsStatic) Band = "20m";
        if (!FreqIsStatic) FrequencyMhz = null;
        if (!ModeIsStatic) Mode = "SSB";
        if (!SubModeIsStatic) SubMode = null;
        if (!RstSentIsStatic) RstSent = "59";
        if (!RstRcvdIsStatic) RstRcvd = "59";
        if (!OpIsStatic) Op = null;
        if (!TxPowerIsStatic) TxPowerWatts = null;
        if (!MyGridIsStatic) MyGridSquare = null;
        if (!MyStateIsStatic) MyState = null;
        if (!MySotaIsStatic) MySotaRef = null;
        if (!MyPotaIsStatic) MySigInfo = null;

        Name = null;
        GridSquare = null;
        City = null;
        State = null;
        County = null;
        Country = null;
        ArrlSection = null;
        CqZone = null;
        ItuZone = null;
        SotaRef = null;
        SigInfo = null;
        SkccNr = null;
        SelectedPrecedenceOption = null;
        Check = null;
        QsoClass = null;
        Comment = null;
        FrequencyRxMhz = null;
        Continent = null;
        QslSent = QslStatus.NotSent;
        QslRcvd = QslStatus.NotSent;
        LotwQslSent = QslStatus.NotSent;
        LotwQslRcvd = QslStatus.NotSent;
        QslViaCallsign = null;
    }
}
