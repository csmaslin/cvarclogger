using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Models;
using CvarcLogger.Core.Rig;
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
    private readonly SettingsService _settings;
    private readonly DialogService _dialogService;
    private readonly IClock _clock;
    private readonly GridTrackerBroadcastService _gridTrackerBroadcast;
    private readonly DispatcherTimer _catPollTimer;
    private readonly DispatcherTimer _liveClockTimer;
    private string? _lastLookedUpCallsign;

    /// <summary>Guards QsoDateTimeUtcText/QsoDateTimeLocalText's bidirectional sync (see
    /// OnQsoDateTimeUtcTextChanged/OnQsoDateTimeLocalTextChanged) against re-entrant feedback -- setting
    /// one field programmatically from the other must not bounce back and forth.</summary>
    private bool _isSyncingDateTime;

    /// <summary>Set around _liveClockTimer's own writes to QsoDateTimeUtcText so the change handler below
    /// can tell "the clock ticked" apart from "the operator typed a digit" -- only the latter should stop
    /// the live clock.</summary>
    private bool _isLiveClockUpdate;

    /// <summary>True once the operator has typed into either date/time field for the QSO currently being
    /// entered. Stops _liveClockTimer from overwriting a manually-entered (e.g. backdated) time; reset
    /// back to false in InitializeAsync/ResetForNextQso so the next QSO starts live-ticking again.</summary>
    private bool _dateTimeManuallyEdited;

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

    public void NotifyFieldVisibilityChanged() => FieldVisibilityChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Whether the entry-form field for this column key should be shown, mirroring
    /// QsoLogViewModel.IsColumnVisible's own logic against the same underlying setting. Only ever
    /// consulted for optional/supplementary fields (see QsoEntryView.xaml.cs) -- Callsign, Station,
    /// Date/Time, Local Time, Band, and Mode are always shown regardless, since those are needed to
    /// log any QSO at all.</summary>
    public bool IsFieldVisible(string key) => !_settings.HiddenLogColumns.Contains(key);

    [ObservableProperty] private string callsign = string.Empty;
    [ObservableProperty] private string qsoDateTimeUtcText = string.Empty;
    [ObservableProperty] private string qsoDateTimeLocalText = string.Empty;
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
    [ObservableProperty] private string? mySotaRef;
    [ObservableProperty] private string? sotaRef;
    [ObservableProperty] private string? mySigInfo;
    [ObservableProperty] private string? sigInfo;
    // Op/Qth describe the operator's own setup, so -- like Band/Mode/RstSent/RstRcvd -- they're sticky
    // rather than reset in ResetForNextQso; TxPowerWatts is the same, since a station typically runs at
    // one consistent power for a whole operating session rather than changing it contact to contact.
    [ObservableProperty] private string? op;
    [ObservableProperty] private string? qth;
    [ObservableProperty] private string? txPowerWatts;
    [ObservableProperty] private string? comment;
    [ObservableProperty] private bool isLookingUp;
    [ObservableProperty] private StationProfile? selectedStationProfile;
    [ObservableProperty] private bool isCatConnected;
    [ObservableProperty] private bool isCatAutoFillPaused;
    [ObservableProperty] private string? catStatusMessage;
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
        SettingsService settings,
        DialogService dialogService,
        IClock clock,
        GridTrackerBroadcastService gridTrackerBroadcast)
    {
        _qsoRepository = qsoRepository;
        _stationProfileRepository = stationProfileRepository;
        _entityResolver = entityResolver;
        _gridZoneResolver = gridZoneResolver;
        _lookupCoordinator = lookupCoordinator;
        _rigCoordinator = rigCoordinator;
        _settings = settings;
        _dialogService = dialogService;
        _clock = clock;
        _gridTrackerBroadcast = gridTrackerBroadcast;

        _catPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _catPollTimer.Tick += OnCatPollTick;

        _liveClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveClockTimer.Tick += OnLiveClockTick;
        _liveClockTimer.Start();
    }

    /// <summary>Keeps QsoDateTimeUtcText (and, via the existing sync, QsoDateTimeLocalText) advancing to
    /// the actual current time while the entry form sits idle -- runs continuously rather than only at
    /// InitializeAsync/ResetForNextQso, matching how ham radio loggers conventionally show a live clock.
    /// Stops touching the field the moment the operator types a manual/backdated time (see
    /// _dateTimeManuallyEdited) so a deliberate edit is never clobbered.</summary>
    private void OnLiveClockTick(object? sender, EventArgs e)
    {
        if (_dateTimeManuallyEdited) return;

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
    /// recomputes UTC.</summary>
    partial void OnQsoDateTimeLocalTextChanged(string value)
    {
        if (_isSyncingDateTime) return;
        _dateTimeManuallyEdited = true;
        if (!TryParseQsoDateTime(value, out var local)) return;

        _isSyncingDateTime = true;
        try
        {
            // Always the short form here: this handler only ever runs for a genuine manual edit of the
            // Local field (sync-driven writes are caught by the _isSyncingDateTime guard above), and the
            // live clock never drives this field directly -- see OnQsoDateTimeUtcTextChanged.
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
            await _rigCoordinator.DisconnectAsync();
            IsCatConnected = false;
            CatStatusMessage = "CAT disconnected.";
            return;
        }

        var (success, error) = await _rigCoordinator.ConnectAsync();
        IsCatConnected = success;
        CatStatusMessage = success ? "CAT connected." : $"CAT connect failed: {error}";
        if (success) _catPollTimer.Start();
    }

    private async void OnCatPollTick(object? sender, EventArgs e)
    {
        // Frequency/Band/Mode/TX Power are the only fields CAT ever writes — they should keep tracking
        // the live radio state even while the operator is mid-way typing the next contact's callsign.
        // "Pause auto-fill" is the deliberate, explicit control for stopping that.
        if (IsCatAutoFillPaused) return;

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
            TxPowerWatts = decimal.TryParse(TxPowerWatts, out var txPower) ? txPower : null,
            Comment = Comment,
            StationProfileId = SelectedStationProfile.Id,
            StationCallsign = SelectedStationProfile.Callsign,
            OperatorCallsign = SelectedStationProfile.OperatorCallsign,
            MyGridSquare = SelectedStationProfile.MyGridSquare,
            MyState = SelectedStationProfile.MyState,
            MyCounty = SelectedStationProfile.MyCounty,
            // Qth/Op default from the station profile (see SeedStationDefaults) but are editable on the
            // entry form, so this QSO gets whatever the operator left them as, not necessarily the
            // profile's own value.
            Qth = Qth,
            Op = Op,
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

    private void ResetForNextQso()
    {
        _lastLookedUpCallsign = null;
        Callsign = string.Empty;

        _dateTimeManuallyEdited = false;
        _isLiveClockUpdate = true;
        try
        {
            QsoDateTimeUtcText = _clock.UtcNow.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isLiveClockUpdate = false;
        }

        FrequencyMhz = null;
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
        Comment = null;
    }
}
