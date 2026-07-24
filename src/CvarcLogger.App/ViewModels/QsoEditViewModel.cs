using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.ViewModels;

public partial class QsoEditViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly LookupCoordinator _lookupCoordinator;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly IGridZoneResolver _gridZoneResolver;
    private readonly DialogService _dialogService;
    private readonly GridTrackerBroadcastService _gridTrackerBroadcast;
    private Qso? _qso;

    /// <summary>Guards QsoDateTimeUtcText/LocalDateTimeText's bidirectional sync against re-entrant
    /// feedback -- same rationale as QsoEntryViewModel's identical guard.</summary>
    private bool _isSyncingDateTime;

    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public event EventHandler? Saved;

    [ObservableProperty] private string qsoDateTimeUtcText = string.Empty;
    /// <summary>Local-time view of QsoDateTimeUtcText, computed from UtcOffsetHours/
    /// ObservesDaylightSavingTime (see OnQsoDateTimeUtcTextChanged) -- not an independently persisted
    /// field, same relationship as Qso.LocalDateTimeOn. Editing it recomputes QsoDateTimeUtcText instead.</summary>
    [ObservableProperty] private string localDateTimeText = string.Empty;
    [ObservableProperty] private string callsign = string.Empty;
    [ObservableProperty] private string band = string.Empty;
    [ObservableProperty] private string mode = string.Empty;
    [ObservableProperty] private string? subMode;
    [ObservableProperty] private string? frequencyMhz;
    [ObservableProperty] private string? frequencyRxMhz;
    [ObservableProperty] private string? rstSent;
    [ObservableProperty] private string? rstRcvd;
    [ObservableProperty] private string? name;
    [ObservableProperty] private string? gridSquare;
    [ObservableProperty] private string? city;
    [ObservableProperty] private string? state;
    [ObservableProperty] private string? county;
    [ObservableProperty] private string? country;
    [ObservableProperty] private string? arrlSection;
    [ObservableProperty] private string? continent;
    [ObservableProperty] private string? cqZone;
    [ObservableProperty] private string? ituZone;
    [ObservableProperty] private string? mySotaRef;
    [ObservableProperty] private string? sotaRef;
    [ObservableProperty] private string? mySigInfo;
    [ObservableProperty] private string? sigInfo;
    [ObservableProperty] private string? txPowerWatts;
    [ObservableProperty] private string? comment;
    [ObservableProperty] private QslStatus qslSent;
    [ObservableProperty] private QslStatus qslRcvd;
    [ObservableProperty] private string? qslSentDateText;
    [ObservableProperty] private string? qslRcvdDateText;
    [ObservableProperty] private QslStatus lotwQslSent;
    [ObservableProperty] private QslStatus lotwQslRcvd;
    [ObservableProperty] private string? lotwQslSentDateText;
    [ObservableProperty] private string? lotwQslRcvdDateText;
    [ObservableProperty] private string? qslViaCallsign;
    [ObservableProperty] private string stationCallsign = string.Empty;
    [ObservableProperty] private string? operatorCallsign;
    [ObservableProperty] private string? myGridSquare;
    [ObservableProperty] private string? myState;
    [ObservableProperty] private string? myCounty;
    [ObservableProperty] private string? qth;
    [ObservableProperty] private string? op;
    [ObservableProperty] private string utcOffsetHours = "0";
    [ObservableProperty] private bool observesDaylightSavingTime;
    [ObservableProperty] private bool isLookingUp;

    public ObservableCollection<string> Bands { get; } = new(QsoFieldOptions.Bands);
    public ObservableCollection<string> Modes { get; } = new(QsoFieldOptions.Modes);

    /// <summary>Sub-Mode picker contents — swapped between QsoFieldOptions.PskSubModes,
    /// .DigitalVoiceSubModes, and .SsbSubModes as Mode changes, see OnModeChanged. Load() re-syncs this
    /// once the QSO's actual Mode is known.</summary>
    public ObservableCollection<string> SubModes { get; } = new(QsoFieldOptions.PskSubModes);

    public Array QslStatuses { get; } = Enum.GetValues(typeof(QslStatus));

    public QsoEditViewModel(
        IQsoRepository qsoRepository,
        LookupCoordinator lookupCoordinator,
        ICallsignEntityResolver entityResolver,
        IGridZoneResolver gridZoneResolver,
        DialogService dialogService,
        GridTrackerBroadcastService gridTrackerBroadcast)
    {
        _qsoRepository = qsoRepository;
        _lookupCoordinator = lookupCoordinator;
        _entityResolver = entityResolver;
        _gridZoneResolver = gridZoneResolver;
        _dialogService = dialogService;
        _gridTrackerBroadcast = gridTrackerBroadcast;
    }

    public void Load(Qso qso)
    {
        _qso = qso;

        // Seed the two date fields together with their offset/DST basis, with the UTC<->Local auto-sync
        // (and the offset/DST resync) suppressed. All four come straight from the stored QSO and are
        // already consistent. If a handler were allowed to fire mid-load it would recompute UTC from
        // Local (or vice versa) before UtcOffsetHours is in place -- i.e. using a zero offset -- and
        // clobber the authoritative UTC time. That was the cause of an edited QSO saving with a shifted
        // time instead of the one originally recorded (and drifting further on each re-edit).
        _isSyncingDateTime = true;
        try
        {
            UtcOffsetHours = qso.UtcOffsetHours?.ToString(CultureInfo.InvariantCulture) ?? "0";
            ObservesDaylightSavingTime = qso.ObservesDaylightSavingTime;
            QsoDateTimeUtcText = qso.QsoDateTimeOnUtc.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            LocalDateTimeText = qso.LocalDateTimeOn.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }

        Callsign = qso.Callsign;
        Band = qso.Band;
        Mode = qso.Mode;
        SubMode = qso.SubMode;
        FrequencyMhz = qso.FrequencyMhz?.ToString("0.######", CultureInfo.InvariantCulture);
        FrequencyRxMhz = qso.FrequencyRxMhz?.ToString("0.######", CultureInfo.InvariantCulture);
        RstSent = qso.RstSent;
        RstRcvd = qso.RstRcvd;
        Name = qso.Name;
        GridSquare = qso.GridSquare;
        City = qso.City;
        State = qso.State;
        County = qso.County;
        Country = qso.Country;
        ArrlSection = qso.ArrlSection;
        Continent = qso.Continent;
        CqZone = qso.CqZone?.ToString(CultureInfo.InvariantCulture);
        ItuZone = qso.ItuZone?.ToString(CultureInfo.InvariantCulture);
        MySotaRef = qso.MySotaRef;
        SotaRef = qso.SotaRef;
        MySigInfo = qso.MySigInfo;
        SigInfo = qso.SigInfo;
        TxPowerWatts = qso.TxPowerWatts?.ToString("0.###", CultureInfo.InvariantCulture);
        Comment = qso.Comment;
        QslSent = qso.QslSent;
        QslRcvd = qso.QslRcvd;
        QslSentDateText = qso.QslSentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        QslRcvdDateText = qso.QslRcvdDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        LotwQslSent = qso.LotwQslSent;
        LotwQslRcvd = qso.LotwQslRcvd;
        LotwQslSentDateText = qso.LotwQslSentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        LotwQslRcvdDateText = qso.LotwQslRcvdDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        QslViaCallsign = qso.QslViaCallsign;
        StationCallsign = qso.StationCallsign;
        OperatorCallsign = qso.OperatorCallsign;
        MyGridSquare = qso.MyGridSquare;
        MyState = qso.MyState;
        MyCounty = qso.MyCounty;
        Qth = qso.Qth;
        Op = qso.Op;
    }

    /// <summary>Hours to add to a UTC time to get local time, DST-adjusted -- same basis as
    /// Qso.LocalDateTimeOn, computed from this window's own UtcOffsetHours/ObservesDaylightSavingTime
    /// fields (this QSO's saved values, not a live station-profile lookup like QsoEntryViewModel's
    /// equivalent).</summary>
    private double CurrentUtcOffsetHours =>
        (double)(decimal.TryParse(UtcOffsetHours, NumberStyles.Number, CultureInfo.InvariantCulture, out var offset) ? offset : 0m)
        + (ObservesDaylightSavingTime ? 1 : 0);

    /// <summary>Typing a UTC date/time recomputes Local Date/Time to match -- mirrors
    /// QsoEntryViewModel's identical sync.</summary>
    partial void OnQsoDateTimeUtcTextChanged(string value)
    {
        if (_isSyncingDateTime) return;
        if (!DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var utc)) return;

        _isSyncingDateTime = true;
        try
        {
            LocalDateTimeText = utc.AddHours(CurrentUtcOffsetHours).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
    }

    /// <summary>Mirror of OnQsoDateTimeUtcTextChanged for the other direction -- typing Local Date/Time
    /// recomputes UTC.</summary>
    partial void OnLocalDateTimeTextChanged(string value)
    {
        if (_isSyncingDateTime) return;
        if (!DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return;

        _isSyncingDateTime = true;
        try
        {
            QsoDateTimeUtcText = local.AddHours(-CurrentUtcOffsetHours).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
    }

    /// <summary>Editing the UTC offset or DST flag changes the local-time basis -- re-derive Local
    /// Date/Time from the still-authoritative UTC text rather than leaving it stale.</summary>
    partial void OnUtcOffsetHoursChanged(string value) => ResyncLocalFromUtc();
    partial void OnObservesDaylightSavingTimeChanged(bool value) => ResyncLocalFromUtc();

    private void ResyncLocalFromUtc()
    {
        // Respect the load-time suppression flag: while Load seeds the fields, the offset/DST setters must
        // not recompute Local from a not-yet-fully-loaded UTC value.
        if (_isSyncingDateTime) return;
        if (!DateTime.TryParseExact(QsoDateTimeUtcText, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var utc)) return;

        _isSyncingDateTime = true;
        try
        {
            LocalDateTimeText = utc.AddHours(CurrentUtcOffsetHours).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
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

    /// <summary>Looks up the callsign and fills in only currently-blank fields — an edit-window lookup
    /// must never clobber data that's already in the log (unlike QsoEntryViewModel's lookup, which runs
    /// against a fresh, empty form).</summary>
    [RelayCommand]
    private async Task LookupAsync()
    {
        if (string.IsNullOrWhiteSpace(Callsign))
        {
            _dialogService.ShowError("Enter a callsign before looking it up.");
            return;
        }

        IsLookingUp = true;
        try
        {
            string normalizedCallsign = Callsign.Trim().ToUpperInvariant();
            var result = await _lookupCoordinator.LookupAsync(normalizedCallsign);
            if (result.Found)
            {
                if (string.IsNullOrWhiteSpace(Name)) Name = result.Name;
                if (string.IsNullOrWhiteSpace(GridSquare)) GridSquare = result.GridSquare;
                if (string.IsNullOrWhiteSpace(Country)) Country = result.Country;
                if (string.IsNullOrWhiteSpace(State)) State = result.State;
                if (string.IsNullOrWhiteSpace(County)) County = result.County;
                if (string.IsNullOrWhiteSpace(City)) City = result.City;
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                _dialogService.ShowError($"Lookup failed: {result.Error}");
            }

            // ARRL Section is derived from State/County rather than looked up directly -- see
            // ArrlSectionResolver for the state/county table and its accuracy caveats. Blank-only,
            // same as the rest of this method.
            if (string.IsNullOrWhiteSpace(ArrlSection)) ArrlSection = ArrlSectionResolver.Resolve(State, County);

            // CQ/ITU zone: prefer resolving from the grid square (accurate to the station's actual
            // location, unlike the DXCC entity's single nominal zone per country — see
            // QsoEntryViewModel.PerformLookupAsync for the full rationale). Blank-only, same as the
            // rest of this method — this is an already-logged QSO, not a fresh entry.
            var resolvedEntity = await _entityResolver.ResolveAsync(normalizedCallsign);
            var (gridCqZone, gridItuZone) = _gridZoneResolver.Resolve(GridSquare);
            if (string.IsNullOrWhiteSpace(CqZone))
                CqZone = gridCqZone?.ToString(CultureInfo.InvariantCulture) ?? resolvedEntity?.CqZone?.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(ItuZone))
                ItuZone = gridItuZone?.ToString(CultureInfo.InvariantCulture) ?? resolvedEntity?.ItuZone?.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_qso is null) return;

        if (string.IsNullOrWhiteSpace(Callsign))
        {
            _dialogService.ShowError("Callsign cannot be empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(StationCallsign))
        {
            _dialogService.ShowError("Station Callsign cannot be empty.");
            return;
        }

        if (!DateTime.TryParseExact(QsoDateTimeUtcText, DateTimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var qsoDateTime))
        {
            _dialogService.ShowError("Date/Time (UTC) must be in the format yyyy-MM-dd HH:mm.");
            return;
        }

        // QsoDateTimeOffUtc is no longer editable from this window -- Local Date/Time replaced that
        // field's UI slot (see LocalDateTimeText) and isn't an independently persisted value, so
        // whatever QsoDateTimeOffUtc already held (e.g. from ADIF import) is left untouched here.
        _qso.QsoDateTimeOnUtc = DateTime.SpecifyKind(qsoDateTime, DateTimeKind.Utc);
        _qso.Callsign = Callsign.Trim().ToUpperInvariant();
        _qso.Band = Band;
        _qso.Mode = Mode;
        _qso.SubMode = string.Equals(Mode, "DATA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Mode, "SSB", StringComparison.OrdinalIgnoreCase) ? SubMode : null;
        _qso.FrequencyMhz = decimal.TryParse(FrequencyMhz, NumberStyles.Number, CultureInfo.InvariantCulture, out var freq) ? freq : null;
        _qso.FrequencyRxMhz = decimal.TryParse(FrequencyRxMhz, NumberStyles.Number, CultureInfo.InvariantCulture, out var freqRx) ? freqRx : null;
        _qso.RstSent = RstSent;
        _qso.RstRcvd = RstRcvd;
        _qso.Name = Name;
        _qso.GridSquare = GridSquare;
        _qso.City = City;
        _qso.State = State?.Trim().ToUpperInvariant();
        _qso.County = County;
        _qso.Country = Country;
        _qso.ArrlSection = ArrlSection?.Trim().ToUpperInvariant();
        _qso.Continent = Continent;
        _qso.CqZone = int.TryParse(CqZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cqZoneValue) ? cqZoneValue : null;
        _qso.ItuZone = int.TryParse(ItuZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ituZoneValue) ? ituZoneValue : null;
        _qso.MySotaRef = MySotaRef;
        _qso.SotaRef = SotaRef;
        _qso.MySigInfo = MySigInfo;
        _qso.SigInfo = SigInfo;
        _qso.TxPowerWatts = decimal.TryParse(TxPowerWatts, NumberStyles.Number, CultureInfo.InvariantCulture, out var txPower) ? txPower : null;
        _qso.Comment = Comment;
        _qso.QslSent = QslSent;
        _qso.QslRcvd = QslRcvd;
        _qso.QslSentDate = DateTime.TryParseExact(QslSentDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var qslSentDate) ? qslSentDate : null;
        _qso.QslRcvdDate = DateTime.TryParseExact(QslRcvdDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var qslRcvdDate) ? qslRcvdDate : null;
        _qso.LotwQslSent = LotwQslSent;
        _qso.LotwQslRcvd = LotwQslRcvd;
        _qso.LotwQslSentDate = DateTime.TryParseExact(LotwQslSentDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lotwSentDate) ? lotwSentDate : null;
        _qso.LotwQslRcvdDate = DateTime.TryParseExact(LotwQslRcvdDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lotwRcvdDate) ? lotwRcvdDate : null;
        _qso.QslViaCallsign = QslViaCallsign;
        _qso.StationCallsign = StationCallsign.Trim().ToUpperInvariant();
        _qso.OperatorCallsign = OperatorCallsign;
        _qso.MyGridSquare = MyGridSquare;
        _qso.MyState = MyState;
        _qso.MyCounty = MyCounty;
        _qso.Qth = Qth;
        _qso.Op = Op;
        _qso.UtcOffsetHours = decimal.TryParse(UtcOffsetHours, NumberStyles.Number, CultureInfo.InvariantCulture, out var utcOffset) ? utcOffset : null;
        _qso.ObservesDaylightSavingTime = ObservesDaylightSavingTime;

        if (!_qso.DxccEntityOverride)
        {
            var resolvedEntity = await _entityResolver.ResolveAsync(_qso.Callsign);
            if (resolvedEntity is not null) _qso.DxccEntityCode = resolvedEntity.EntityCode;
        }

        await _qsoRepository.UpdateAsync(_qso);
        _gridTrackerBroadcast.BroadcastQso(_qso);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
