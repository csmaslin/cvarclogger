using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcCellLog.Models;
using CvarcCellLog.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Models;
using Microsoft.Maui.Dispatching;

namespace CvarcCellLog.ViewModels;

/// <summary>Adapted from the WPF app's QsoEntryViewModel for Milestone 1 (see the approved MVP plan),
/// then updated in Milestone 2 to add the real Station Profile picker this originally deferred (Milestone
/// 1's plain Preferences-backed station fields are gone -- Qso.StationProfileId is now set for every QSO
/// logged from this app, same as the WPF app) and the online Lookup step (LookupCommand/PerformLookupAsync,
/// ported from the WPF app's same-named members). CAT polling and GridTracker broadcast are still out of
/// scope. The UTC/local date-time sync logic and the ARRL-Section/CQ-zone/ITU-zone auto-resolution now run
/// as part of the lookup, same as the WPF app.</summary>
public partial class QsoEntryViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly IStationProfileRepository _stationProfileRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly IGridZoneResolver _gridZoneResolver;
    private readonly LookupCoordinator _lookupCoordinator;
    private readonly IClock _clock;
    private readonly IDispatcherTimer _liveClockTimer;

    private string? _lastLookedUpCallsign;

    private const string LastUsedStationProfileIdKey = "LastUsedStationProfileId";

    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const string ShortDateTimeFormat = "yyyy-MM-dd HH:mm";
    private static readonly string[] AcceptedDateTimeFormats = { DateTimeFormat, ShortDateTimeFormat };

    /// <summary>Guards QsoDateTimeUtcText/QsoDateTimeLocalText's bidirectional sync against re-entrant
    /// feedback -- same rationale as the WPF app's QsoEntryViewModel.</summary>
    private bool _isSyncingDateTime;
    private bool _isLiveClockUpdate;
    private bool _dateTimeManuallyEdited;

    private static bool TryParseQsoDateTime(string value, out DateTime result) =>
        DateTime.TryParseExact(value.TrimEnd(':', '-', ' '), AcceptedDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

    private static string SyncFormatFor(string sourceText) =>
        DateTime.TryParseExact(sourceText.TrimEnd(':', '-', ' '), DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? DateTimeFormat
            : ShortDateTimeFormat;

    public event EventHandler? QsoLogged;

    [ObservableProperty] private string callsign = string.Empty;
    [ObservableProperty] private string qsoDateTimeUtcText = string.Empty;
    [ObservableProperty] private string qsoDateTimeLocalText = string.Empty;
    [ObservableProperty] private string? qsoDateTimeOffUtcText;
    [ObservableProperty] private string band = "20m";
    [ObservableProperty] private string mode = "SSB";
    [ObservableProperty] private ModeOption selectedModeOption = ModeOptions.For("SSB");
    [ObservableProperty] private string? subMode;
    [ObservableProperty] private string? frequencyMhz;
    // Sticky (not cleared in ResetForNextQso) -- a station typically runs at one consistent power for
    // a whole operating session rather than changing it contact to contact, same rationale as Op/Qth.
    [ObservableProperty] private string? txPowerWatts;
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
    [ObservableProperty] private string? comment;

    // SOTA/POTA activation references. MySotaRef/MySigInfo (the operator's own summit/park) are
    // sticky -- not cleared in ResetForNextQso, same rationale as Band/Mode/RstSent/RstRcvd: they
    // stay constant across a whole activation session. SotaRef/SigInfo describe the *contacted*
    // station, so they reset per QSO like Name/Grid. Matches the WPF app's QsoEntryViewModel exactly.
    [ObservableProperty] private string? mySotaRef;
    [ObservableProperty] private string? sotaRef;
    [ObservableProperty] private string? mySigInfo;
    [ObservableProperty] private string? sigInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    // Starts true to match the "SSB" Mode default above -- OnModeChanged doesn't fire for a field
    // initializer's starting value, same reason SubModes above is pre-populated with SsbSubModes.
    [ObservableProperty] private bool hasSubModes = true;

    // Station identity: a real StationProfile row now (Milestone 2), denormalized onto the Qso at save
    // time just like the WPF app -- see Qso.StationProfileId's doc comment for why (later profile edits
    // must never retroactively rewrite already-logged QSOs).
    [ObservableProperty] private StationProfile? selectedStationProfile;

    [ObservableProperty] private bool isLookingUp;

    public ObservableCollection<StationProfile> StationProfiles { get; } = new();

    public ObservableCollection<string> Bands { get; } = new(QsoFieldOptions.Bands);
    public ObservableCollection<ModeOption> Modes { get; } = new(ModeOptions.All);

    /// <summary>Sub-Mode picker contents -- swapped between QsoFieldOptions.*SubModes as Mode changes,
    /// see OnModeChanged. Starts as SsbSubModes to match the "SSB" default above.</summary>
    public ObservableCollection<string> SubModes { get; } = new(QsoFieldOptions.SsbSubModes);

    private double CurrentUtcOffsetHours =>
        (double)(SelectedStationProfile?.UtcOffsetHours ?? 0m) + (SelectedStationProfile?.ObservesDaylightSavingTime == true ? 1 : 0);

    public QsoEntryViewModel(
        IQsoRepository qsoRepository,
        IStationProfileRepository stationProfileRepository,
        ICallsignEntityResolver entityResolver,
        IGridZoneResolver gridZoneResolver,
        LookupCoordinator lookupCoordinator,
        IClock clock,
        IDispatcher dispatcher)
    {
        _qsoRepository = qsoRepository;
        _stationProfileRepository = stationProfileRepository;
        _entityResolver = entityResolver;
        _gridZoneResolver = gridZoneResolver;
        _lookupCoordinator = lookupCoordinator;
        _clock = clock;

        _liveClockTimer = dispatcher.CreateTimer();
        _liveClockTimer.Interval = TimeSpan.FromSeconds(1);
        _liveClockTimer.Tick += (_, _) => OnLiveClockTick();
        _liveClockTimer.Start();

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

    /// <summary>Keeps QsoDateTimeUtcText (and, via sync, QsoDateTimeLocalText) advancing to the actual
    /// current time while the entry form sits idle. Stops the moment the operator types a manual/
    /// backdated time (see _dateTimeManuallyEdited) so a deliberate edit is never clobbered.</summary>
    private void OnLiveClockTick()
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

    /// <summary>Typing a UTC date/time recomputes the Local Time field to match.</summary>
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

    /// <summary>Mirror of OnQsoDateTimeUtcTextChanged for the other direction.</summary>
    partial void OnQsoDateTimeLocalTextChanged(string value)
    {
        if (_isSyncingDateTime) return;
        _dateTimeManuallyEdited = true;
        if (!TryParseQsoDateTime(value, out var local)) return;

        _isSyncingDateTime = true;
        try
        {
            QsoDateTimeUtcText = local.AddHours(-CurrentUtcOffsetHours).ToString(ShortDateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
    }

    /// <summary>Picker binds to this wrapper (see ModeOption) rather than Mode directly, so the display
    /// can abbreviate "DIGITALVOICE" to "DV" without touching the real ADIF value Mode holds.</summary>
    partial void OnSelectedModeOptionChanged(ModeOption value) => Mode = value.Value;

    /// <summary>Swaps the Sub-Mode picker's contents to match the newly-selected Mode, clearing any
    /// leftover selection that doesn't belong to the new list.</summary>
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
        HasSubModes = SubModes.Count > 0;
    }

    /// <summary>Loads the available station profiles and restores whichever one was last used (falling
    /// back to the repository's default profile, then simply the first one, if there's no remembered
    /// selection) -- mirrors SettingsService.LastUsedStationProfileId in the WPF app, just Preferences-backed
    /// here rather than a full ported SettingsService for one value. Called from QsoEntryPage.OnAppearing
    /// so the picker is populated (and re-populated, in case profiles changed) each time the page is shown.</summary>
    public async Task InitializeAsync()
    {
        var profiles = await _stationProfileRepository.GetAllAsync();
        StationProfiles.Clear();
        foreach (var p in profiles) StationProfiles.Add(p);

        if (StationProfiles.Count == 0)
        {
            SelectedStationProfile = null;
            ErrorMessage = "No station profiles yet -- tap \"Manage Station Profiles\" below to create one before logging a QSO.";
            return;
        }

        int lastUsedId = Preferences.Default.Get(LastUsedStationProfileIdKey, 0);
        SelectedStationProfile = StationProfiles.FirstOrDefault(p => p.Id == lastUsedId)
            ?? StationProfiles.FirstOrDefault(p => p.IsDefault)
            ?? StationProfiles[0];
    }

    /// <summary>Selecting a different profile changes the local-time basis (UTC offset/DST) -- re-derive
    /// Local Time from the still-authoritative UTC text rather than leaving it stale, same rule
    /// QsoEditViewModel applies when its UTC offset/DST fields change. Also remembers the selection for
    /// next launch.</summary>
    partial void OnSelectedStationProfileChanged(StationProfile? value)
    {
        if (value is not null) Preferences.Default.Set(LastUsedStationProfileIdKey, value.Id);

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

    [RelayCommand]
    private async Task LookupAsync()
    {
        if (string.IsNullOrWhiteSpace(Callsign)) return;
        IsLookingUp = true;
        try
        {
            await PerformLookupAsync();
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    /// <summary>Runs the online lookup and fills in currently-empty fields, tracking which callsign was
    /// looked up so LogQsoAsync can tell whether it still needs to run one. Never throws -- lookup
    /// failures are best-effort and silent (a lookup miss must never block logging a QSO), matching the
    /// WPF app's PerformLookupAsync except there's no dialog service here to report failures through.</summary>
    private async Task PerformLookupAsync()
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

        // ARRL Section is derived from State/County rather than looked up directly. CQ/ITU zone prefer
        // resolving from the grid square, falling back to the contact's DXCC entity's nominal zone --
        // neither depends on the online lookup service finding a match, so both run regardless.
        ArrlSection = ArrlSectionResolver.Resolve(State, County) ?? ArrlSection;

        var resolvedEntity = await _entityResolver.ResolveAsync(normalizedCallsign);
        var (gridCqZone, gridItuZone) = _gridZoneResolver.Resolve(GridSquare);
        CqZone = gridCqZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.CqZone?.ToString(CultureInfo.InvariantCulture) ?? CqZone;
        ItuZone = gridItuZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.ItuZone?.ToString(CultureInfo.InvariantCulture) ?? ItuZone;
    }

    [RelayCommand]
    private async Task LogQsoAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Callsign))
        {
            ErrorMessage = "Enter a callsign before logging the QSO.";
            return;
        }
        if (SelectedStationProfile is null)
        {
            ErrorMessage = "Select a station profile before logging the QSO (create one under Station first if none exist).";
            return;
        }
        if (!TryParseQsoDateTime(QsoDateTimeUtcText, out var qsoDateTimeUtc))
        {
            ErrorMessage = "Date/Time (UTC) must be in the format yyyy-MM-dd HH:mm:ss or yyyy-MM-dd HH:mm.";
            return;
        }

        string normalizedCallsign = Callsign.Trim().ToUpperInvariant();

        // Make sure the online lookup has run for this callsign before saving -- covers both "never
        // tapped Lookup" and "tapped Lookup, then edited the callsign afterward."
        if (!string.Equals(_lastLookedUpCallsign, normalizedCallsign, StringComparison.OrdinalIgnoreCase))
        {
            IsLookingUp = true;
            try
            {
                await PerformLookupAsync();
            }
            finally
            {
                IsLookingUp = false;
            }
        }

        var resolvedEntity = await _entityResolver.ResolveAsync(normalizedCallsign);

        DateTime? qsoDateTimeOffUtc = !string.IsNullOrWhiteSpace(QsoDateTimeOffUtcText) && TryParseQsoDateTime(QsoDateTimeOffUtcText, out var offUtc)
            ? DateTime.SpecifyKind(offUtc, DateTimeKind.Utc)
            : null;

        var qso = new Qso
        {
            Callsign = normalizedCallsign,
            QsoDateTimeOnUtc = DateTime.SpecifyKind(qsoDateTimeUtc, DateTimeKind.Utc),
            QsoDateTimeOffUtc = qsoDateTimeOffUtc,
            Band = Band,
            Mode = Mode,
            SubMode = string.Equals(Mode, "DATA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Mode, "SSB", StringComparison.OrdinalIgnoreCase) ? SubMode : null,
            FrequencyMhz = decimal.TryParse(FrequencyMhz, out var freq) ? freq : null,
            TxPowerWatts = decimal.TryParse(TxPowerWatts, out var txPower) ? txPower : null,
            RstSent = RstSent,
            RstRcvd = RstRcvd,
            Name = Name,
            GridSquare = GridSquare,
            City = City,
            State = string.IsNullOrWhiteSpace(State) ? null : State.Trim().ToUpperInvariant(),
            County = County,
            Country = Country,
            ArrlSection = string.IsNullOrWhiteSpace(ArrlSection) ? null : ArrlSection.Trim().ToUpperInvariant(),
            CqZone = int.TryParse(CqZone, out var cqZoneValue) ? cqZoneValue : null,
            ItuZone = int.TryParse(ItuZone, out var ituZoneValue) ? ituZoneValue : null,
            Comment = Comment,
            MySotaRef = MySotaRef,
            SotaRef = SotaRef,
            MySigInfo = MySigInfo,
            SigInfo = SigInfo,
            StationProfileId = SelectedStationProfile.Id,
            StationCallsign = SelectedStationProfile.Callsign,
            OperatorCallsign = SelectedStationProfile.OperatorCallsign,
            MyGridSquare = SelectedStationProfile.MyGridSquare,
            MyState = SelectedStationProfile.MyState,
            MyCounty = SelectedStationProfile.MyCounty,
            Qth = SelectedStationProfile.Qth,
            Op = SelectedStationProfile.Op,
            UtcOffsetHours = SelectedStationProfile.UtcOffsetHours,
            ObservesDaylightSavingTime = SelectedStationProfile.ObservesDaylightSavingTime,
        };

        if (resolvedEntity is not null)
        {
            qso.DxccEntityCode = resolvedEntity.EntityCode;
            qso.Country = string.IsNullOrWhiteSpace(qso.Country) ? resolvedEntity.EntityName : qso.Country;
            qso.Continent ??= resolvedEntity.Continent;
            qso.CqZone ??= resolvedEntity.CqZone;
            qso.ItuZone ??= resolvedEntity.ItuZone;
        }

        await _qsoRepository.AddAsync(qso);

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

        QsoDateTimeOffUtcText = null;
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
        Comment = null;
        SotaRef = null;
        SigInfo = null;
    }
}
