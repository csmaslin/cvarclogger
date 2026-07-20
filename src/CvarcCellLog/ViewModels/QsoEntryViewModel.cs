using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Models;
using Microsoft.Maui.Dispatching;

namespace CvarcCellLog.ViewModels;

/// <summary>Adapted from the WPF app's QsoEntryViewModel for Milestone 1 (see the approved MVP plan):
/// drops CAT polling, online lookup, and GridTracker broadcast entirely, and replaces the WPF app's
/// Station Profile picker with a handful of plain, Preferences-backed station-identity fields (sticky
/// across QSOs and app restarts, but not a StationProfile database row -- Qso.StationProfileId stays
/// null for every QSO logged from this app). The UTC/local date-time sync logic and the
/// ARRL-Section/CQ-zone/ITU-zone auto-resolution are kept, just run at save time here since there's no
/// separate online-lookup step to trigger them earlier.</summary>
public partial class QsoEntryViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly IGridZoneResolver _gridZoneResolver;
    private readonly IClock _clock;
    private readonly IDispatcherTimer _liveClockTimer;

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
    [ObservableProperty] private string band = "20m";
    [ObservableProperty] private string mode = "SSB";
    [ObservableProperty] private string? subMode;
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
    [ObservableProperty] private string? comment;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    // Starts true to match the "SSB" Mode default above -- OnModeChanged doesn't fire for a field
    // initializer's starting value, same reason SubModes above is pre-populated with SsbSubModes.
    [ObservableProperty] private bool hasSubModes = true;

    // Station identity: plain fields backed by Preferences (not a StationProfile row -- see Milestone 1
    // plan's "Station identity" decision), sticky across QSOs and app restarts.
    [ObservableProperty] private string stationCallsign = string.Empty;
    [ObservableProperty] private string? operatorCallsign;
    [ObservableProperty] private string? myGridSquare;
    [ObservableProperty] private string? myState;
    [ObservableProperty] private string? myCounty;
    [ObservableProperty] private string? qth;
    [ObservableProperty] private string? op;
    [ObservableProperty] private string utcOffsetHoursText = "0";
    [ObservableProperty] private bool observesDaylightSavingTime;

    public ObservableCollection<string> Bands { get; } = new(QsoFieldOptions.Bands);
    public ObservableCollection<string> Modes { get; } = new(QsoFieldOptions.Modes);

    /// <summary>Sub-Mode picker contents -- swapped between QsoFieldOptions.*SubModes as Mode changes,
    /// see OnModeChanged. Starts as SsbSubModes to match the "SSB" default above.</summary>
    public ObservableCollection<string> SubModes { get; } = new(QsoFieldOptions.SsbSubModes);

    private double CurrentUtcOffsetHours =>
        (decimal.TryParse(UtcOffsetHoursText, out var offset) ? (double)offset : 0) + (ObservesDaylightSavingTime ? 1 : 0);

    public QsoEntryViewModel(
        IQsoRepository qsoRepository,
        ICallsignEntityResolver entityResolver,
        IGridZoneResolver gridZoneResolver,
        IClock clock,
        IDispatcher dispatcher)
    {
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
        _gridZoneResolver = gridZoneResolver;
        _clock = clock;

        _liveClockTimer = dispatcher.CreateTimer();
        _liveClockTimer.Interval = TimeSpan.FromSeconds(1);
        _liveClockTimer.Tick += (_, _) => OnLiveClockTick();
        _liveClockTimer.Start();

        LoadStationDefaults();

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

    private void LoadStationDefaults()
    {
        StationCallsign = Preferences.Default.Get(nameof(StationCallsign), string.Empty);
        OperatorCallsign = NullIfEmpty(Preferences.Default.Get(nameof(OperatorCallsign), string.Empty));
        MyGridSquare = NullIfEmpty(Preferences.Default.Get(nameof(MyGridSquare), string.Empty));
        MyState = NullIfEmpty(Preferences.Default.Get(nameof(MyState), string.Empty));
        MyCounty = NullIfEmpty(Preferences.Default.Get(nameof(MyCounty), string.Empty));
        Qth = NullIfEmpty(Preferences.Default.Get(nameof(Qth), string.Empty));
        Op = NullIfEmpty(Preferences.Default.Get(nameof(Op), string.Empty));
        UtcOffsetHoursText = Preferences.Default.Get(nameof(UtcOffsetHoursText), "0");
        ObservesDaylightSavingTime = Preferences.Default.Get(nameof(ObservesDaylightSavingTime), false);
    }

    private void SaveStationDefaults()
    {
        Preferences.Default.Set(nameof(StationCallsign), StationCallsign);
        Preferences.Default.Set(nameof(OperatorCallsign), OperatorCallsign ?? string.Empty);
        Preferences.Default.Set(nameof(MyGridSquare), MyGridSquare ?? string.Empty);
        Preferences.Default.Set(nameof(MyState), MyState ?? string.Empty);
        Preferences.Default.Set(nameof(MyCounty), MyCounty ?? string.Empty);
        Preferences.Default.Set(nameof(Qth), Qth ?? string.Empty);
        Preferences.Default.Set(nameof(Op), Op ?? string.Empty);
        Preferences.Default.Set(nameof(UtcOffsetHoursText), UtcOffsetHoursText);
        Preferences.Default.Set(nameof(ObservesDaylightSavingTime), ObservesDaylightSavingTime);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [RelayCommand]
    private async Task LogQsoAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Callsign))
        {
            ErrorMessage = "Enter a callsign before logging the QSO.";
            return;
        }
        if (string.IsNullOrWhiteSpace(StationCallsign))
        {
            ErrorMessage = "Enter your station callsign before logging the QSO.";
            return;
        }
        if (!TryParseQsoDateTime(QsoDateTimeUtcText, out var qsoDateTimeUtc))
        {
            ErrorMessage = "Date/Time (UTC) must be in the format yyyy-MM-dd HH:mm:ss or yyyy-MM-dd HH:mm.";
            return;
        }

        string normalizedCallsign = Callsign.Trim().ToUpperInvariant();

        // ARRL Section is derived from State/County rather than looked up directly. CQ/ITU zone prefer
        // resolving from the grid square, falling back to the contact's DXCC entity's nominal zone --
        // same rule the desktop app applies at Lookup time, just run here at save time since there's no
        // separate online-lookup step in this MVP.
        ArrlSection = ArrlSectionResolver.Resolve(State, County) ?? ArrlSection;

        var resolvedEntity = await _entityResolver.ResolveAsync(normalizedCallsign);
        var (gridCqZone, gridItuZone) = _gridZoneResolver.Resolve(GridSquare);
        CqZone = gridCqZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.CqZone?.ToString(CultureInfo.InvariantCulture) ?? CqZone;
        ItuZone = gridItuZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.ItuZone?.ToString(CultureInfo.InvariantCulture) ?? ItuZone;

        var qso = new Qso
        {
            Callsign = normalizedCallsign,
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
            State = string.IsNullOrWhiteSpace(State) ? null : State.Trim().ToUpperInvariant(),
            County = County,
            Country = Country,
            ArrlSection = string.IsNullOrWhiteSpace(ArrlSection) ? null : ArrlSection.Trim().ToUpperInvariant(),
            CqZone = int.TryParse(CqZone, out var cqZoneValue) ? cqZoneValue : null,
            ItuZone = int.TryParse(ItuZone, out var ituZoneValue) ? ituZoneValue : null,
            Comment = Comment,
            StationProfileId = null,
            StationCallsign = StationCallsign.Trim().ToUpperInvariant(),
            OperatorCallsign = OperatorCallsign,
            MyGridSquare = MyGridSquare,
            MyState = MyState,
            MyCounty = MyCounty,
            Qth = Qth,
            Op = Op,
            UtcOffsetHours = decimal.TryParse(UtcOffsetHoursText, out var utcOffset) ? utcOffset : 0m,
            ObservesDaylightSavingTime = ObservesDaylightSavingTime,
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
        SaveStationDefaults();

        QsoLogged?.Invoke(this, EventArgs.Empty);
        ResetForNextQso();
    }

    private void ResetForNextQso()
    {
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
        Comment = null;
    }
}
