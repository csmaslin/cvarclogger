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

namespace CvarcCellLog.ViewModels;

/// <summary>Adapted from the WPF app's QsoEditViewModel for Milestone 1: mirrors QsoEntryViewModel's
/// trimmed field set (no QSL/LoTW tracking -- out of scope, matching what the entry form
/// captures; SOTA/POTA/TX-Power/Time-Off fields were added later). ARRL Section/CQ/ITU-zone re-resolution runs at save time, same as
/// QsoEntryViewModel. A manual Lookup command was added in Milestone 2 (mirroring QsoEntryViewModel's,
/// minus the "auto-run at save if not yet looked up" behavior -- an edit screen already has real
/// contact data loaded, so there's no equivalent "never skip it" requirement here, just an optional
/// re-lookup action). Shell passes only a QSO id via QueryProperty (no direct object handoff between
/// pages), so LoadAsync fetches the entity itself.</summary>
[QueryProperty(nameof(QsoId), "id")]
public partial class QsoEditViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly IGridZoneResolver _gridZoneResolver;
    private readonly LookupCoordinator _lookupCoordinator;
    private Qso? _qso;

    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const string ShortDateTimeFormat = "yyyy-MM-dd HH:mm";
    private static readonly string[] AcceptedDateTimeFormats = { DateTimeFormat, ShortDateTimeFormat };

    /// <summary>Guards QsoDateTimeUtcText/QsoDateTimeLocalText's bidirectional sync against re-entrant
    /// feedback -- same rationale as QsoEntryViewModel's identical guard.</summary>
    private bool _isSyncingDateTime;

    private static bool TryParseQsoDateTime(string value, out DateTime result) =>
        DateTime.TryParseExact(value.TrimEnd(':', '-', ' '), AcceptedDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

    public event EventHandler? Saved;
    public event EventHandler? Deleted;

    [ObservableProperty] private int qsoId;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoaded))]
    private bool isLoading = true;

    public bool IsLoaded => !IsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty] private string callsign = string.Empty;
    [ObservableProperty] private string qsoDateTimeUtcText = string.Empty;
    [ObservableProperty] private string qsoDateTimeLocalText = string.Empty;
    [ObservableProperty] private string? qsoDateTimeOffUtcText;
    [ObservableProperty] private string band = string.Empty;
    [ObservableProperty] private string mode = string.Empty;
    [ObservableProperty] private ModeOption selectedModeOption = ModeOptions.For(string.Empty);
    [ObservableProperty] private string? subMode;
    [ObservableProperty] private string? frequencyMhz;
    [ObservableProperty] private string? txPowerWatts;
    [ObservableProperty] private string? rstSent;
    [ObservableProperty] private string? rstRcvd;
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
    [ObservableProperty] private string? mySotaRef;
    [ObservableProperty] private string? sotaRef;
    [ObservableProperty] private string? mySigInfo;
    [ObservableProperty] private string? sigInfo;
    [ObservableProperty] private bool hasSubModes;

    [ObservableProperty] private string stationCallsign = string.Empty;
    [ObservableProperty] private string? operatorCallsign;
    [ObservableProperty] private string? myGridSquare;
    [ObservableProperty] private string? myState;
    [ObservableProperty] private string? myCounty;
    [ObservableProperty] private string? qth;
    [ObservableProperty] private string? op;
    [ObservableProperty] private string utcOffsetHoursText = "0";
    [ObservableProperty] private bool observesDaylightSavingTime;
    [ObservableProperty] private bool isLookingUp;

    public ObservableCollection<string> Bands { get; } = new(QsoFieldOptions.Bands);
    public ObservableCollection<ModeOption> Modes { get; } = new(ModeOptions.All);
    public ObservableCollection<string> SubModes { get; } = new();

    private double CurrentUtcOffsetHours =>
        (decimal.TryParse(UtcOffsetHoursText, out var offset) ? (double)offset : 0) + (ObservesDaylightSavingTime ? 1 : 0);

    public QsoEditViewModel(
        IQsoRepository qsoRepository,
        ICallsignEntityResolver entityResolver,
        IGridZoneResolver gridZoneResolver,
        LookupCoordinator lookupCoordinator)
    {
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
        _gridZoneResolver = gridZoneResolver;
        _lookupCoordinator = lookupCoordinator;
    }

    partial void OnQsoIdChanged(int value) => _ = LoadAsync(value);

    private async Task LoadAsync(int id)
    {
        IsLoading = true;
        try
        {
            _qso = await _qsoRepository.GetByIdAsync(id);
            if (_qso is null)
            {
                ErrorMessage = "QSO not found.";
                return;
            }

            Callsign = _qso.Callsign;
            QsoDateTimeUtcText = _qso.QsoDateTimeOnUtc.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            QsoDateTimeLocalText = _qso.LocalDateTimeOn.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            QsoDateTimeOffUtcText = _qso.QsoDateTimeOffUtc?.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            Band = _qso.Band;
            SelectedModeOption = ModeOptions.For(_qso.Mode);
            SubMode = _qso.SubMode;
            FrequencyMhz = _qso.FrequencyMhz?.ToString("0.######", CultureInfo.InvariantCulture);
            TxPowerWatts = _qso.TxPowerWatts?.ToString("0.######", CultureInfo.InvariantCulture);
            RstSent = _qso.RstSent;
            RstRcvd = _qso.RstRcvd;
            Name = _qso.Name;
            GridSquare = _qso.GridSquare;
            City = _qso.City;
            State = _qso.State;
            County = _qso.County;
            Country = _qso.Country;
            ArrlSection = _qso.ArrlSection;
            CqZone = _qso.CqZone?.ToString(CultureInfo.InvariantCulture);
            ItuZone = _qso.ItuZone?.ToString(CultureInfo.InvariantCulture);
            Comment = _qso.Comment;
            MySotaRef = _qso.MySotaRef;
            SotaRef = _qso.SotaRef;
            MySigInfo = _qso.MySigInfo;
            SigInfo = _qso.SigInfo;
            StationCallsign = _qso.StationCallsign;
            OperatorCallsign = _qso.OperatorCallsign;
            MyGridSquare = _qso.MyGridSquare;
            MyState = _qso.MyState;
            MyCounty = _qso.MyCounty;
            Qth = _qso.Qth;
            Op = _qso.Op;
            UtcOffsetHoursText = _qso.UtcOffsetHours?.ToString(CultureInfo.InvariantCulture) ?? "0";
            ObservesDaylightSavingTime = _qso.ObservesDaylightSavingTime;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Typing a UTC date/time recomputes the Local Time field to match -- mirrors
    /// QsoEntryViewModel's identical sync (minus the live-clock concern, which doesn't apply here).</summary>
    partial void OnQsoDateTimeUtcTextChanged(string value)
    {
        if (_isSyncingDateTime) return;
        if (!TryParseQsoDateTime(value, out var utc)) return;

        _isSyncingDateTime = true;
        try
        {
            QsoDateTimeLocalText = utc.AddHours(CurrentUtcOffsetHours).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
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
        if (!TryParseQsoDateTime(value, out var local)) return;

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

    /// <summary>Editing the UTC offset or DST flag changes the local-time basis -- re-derive Local Time
    /// from the still-authoritative UTC text rather than leaving it stale.</summary>
    partial void OnUtcOffsetHoursTextChanged(string value) => ResyncLocalFromUtc();
    partial void OnObservesDaylightSavingTimeChanged(bool value) => ResyncLocalFromUtc();

    private void ResyncLocalFromUtc()
    {
        if (!TryParseQsoDateTime(QsoDateTimeUtcText, out var utc)) return;

        _isSyncingDateTime = true;
        try
        {
            QsoDateTimeLocalText = utc.AddHours(CurrentUtcOffsetHours).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSyncingDateTime = false;
        }
    }

    /// <summary>Picker binds to this wrapper (see ModeOption) rather than Mode directly, so the display
    /// can abbreviate "DIGITALVOICE" to "DV" without touching the real ADIF value Mode holds.</summary>
    partial void OnSelectedModeOptionChanged(ModeOption value) => Mode = value.Value;

    /// <summary>Swaps the Sub-Mode picker's contents to match the newly-selected Mode -- mirrors
    /// QsoEntryViewModel's identical logic.</summary>
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

    /// <summary>Re-runs the online lookup for this QSO's callsign, filling in currently-blank fields --
    /// mirrors QsoEntryViewModel's LookupAsync/PerformLookupAsync. Useful when a QSO was logged without
    /// a successful lookup at the time (e.g. no network) and the operator wants to fill in the gaps
    /// later.</summary>
    [RelayCommand]
    private async Task LookupAsync()
    {
        if (string.IsNullOrWhiteSpace(Callsign)) return;

        IsLookingUp = true;
        try
        {
            string normalizedCallsign = Callsign.Trim().ToUpperInvariant();
            var result = await _lookupCoordinator.LookupAsync(normalizedCallsign);

            if (result.Found)
            {
                // Overwrite unconditionally on a successful lookup -- unlike QsoEntryViewModel's
                // fill-blank-only merge (appropriate for a brand-new contact), an edited QSO may already
                // carry stale/incomplete data from a prior lookup or manual entry that a fresh lookup
                // should correct, not skip.
                Name = result.Name ?? Name;
                GridSquare = result.GridSquare ?? GridSquare;
                Country = result.Country ?? Country;
                State = result.State ?? State;
                County = result.County ?? County;
                City = result.City ?? City;
            }

            ArrlSection = ArrlSectionResolver.Resolve(State, County) ?? ArrlSection;

            var resolvedEntity = await _entityResolver.ResolveAsync(normalizedCallsign);
            var (gridCqZone, gridItuZone) = _gridZoneResolver.Resolve(GridSquare);
            CqZone = gridCqZone?.ToString(CultureInfo.InvariantCulture)
                ?? resolvedEntity?.CqZone?.ToString(CultureInfo.InvariantCulture) ?? CqZone;
            ItuZone = gridItuZone?.ToString(CultureInfo.InvariantCulture)
                ?? resolvedEntity?.ItuZone?.ToString(CultureInfo.InvariantCulture) ?? ItuZone;
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
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Callsign))
        {
            ErrorMessage = "Callsign cannot be empty.";
            return;
        }
        if (string.IsNullOrWhiteSpace(StationCallsign))
        {
            ErrorMessage = "Station Callsign cannot be empty.";
            return;
        }
        if (!TryParseQsoDateTime(QsoDateTimeUtcText, out var qsoDateTimeUtc))
        {
            ErrorMessage = "Date/Time (UTC) must be in the format yyyy-MM-dd HH:mm:ss or yyyy-MM-dd HH:mm.";
            return;
        }

        DateTime? qsoDateTimeOffUtc = !string.IsNullOrWhiteSpace(QsoDateTimeOffUtcText) && TryParseQsoDateTime(QsoDateTimeOffUtcText, out var offUtc)
            ? DateTime.SpecifyKind(offUtc, DateTimeKind.Utc)
            : null;

        string normalizedCallsign = Callsign.Trim().ToUpperInvariant();

        // Same re-resolution rules as QsoEntryViewModel.LogQsoAsync -- see there for the full rationale.
        ArrlSection = ArrlSectionResolver.Resolve(State, County) ?? ArrlSection;
        var resolvedEntity = await _entityResolver.ResolveAsync(normalizedCallsign);
        var (gridCqZone, gridItuZone) = _gridZoneResolver.Resolve(GridSquare);
        CqZone = gridCqZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.CqZone?.ToString(CultureInfo.InvariantCulture) ?? CqZone;
        ItuZone = gridItuZone?.ToString(CultureInfo.InvariantCulture)
            ?? resolvedEntity?.ItuZone?.ToString(CultureInfo.InvariantCulture) ?? ItuZone;

        _qso.Callsign = normalizedCallsign;
        _qso.QsoDateTimeOnUtc = DateTime.SpecifyKind(qsoDateTimeUtc, DateTimeKind.Utc);
        _qso.QsoDateTimeOffUtc = qsoDateTimeOffUtc;
        _qso.Band = Band;
        _qso.Mode = Mode;
        _qso.SubMode = string.Equals(Mode, "DATA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Mode, "SSB", StringComparison.OrdinalIgnoreCase) ? SubMode : null;
        _qso.FrequencyMhz = decimal.TryParse(FrequencyMhz, NumberStyles.Number, CultureInfo.InvariantCulture, out var freq) ? freq : null;
        _qso.TxPowerWatts = decimal.TryParse(TxPowerWatts, NumberStyles.Number, CultureInfo.InvariantCulture, out var txPower) ? txPower : null;
        _qso.RstSent = RstSent;
        _qso.RstRcvd = RstRcvd;
        _qso.Name = Name;
        _qso.GridSquare = GridSquare;
        _qso.City = City;
        _qso.State = string.IsNullOrWhiteSpace(State) ? null : State.Trim().ToUpperInvariant();
        _qso.County = County;
        _qso.Country = Country;
        _qso.ArrlSection = string.IsNullOrWhiteSpace(ArrlSection) ? null : ArrlSection.Trim().ToUpperInvariant();
        _qso.CqZone = int.TryParse(CqZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cqZoneValue) ? cqZoneValue : null;
        _qso.ItuZone = int.TryParse(ItuZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ituZoneValue) ? ituZoneValue : null;
        _qso.Comment = Comment;
        _qso.MySotaRef = MySotaRef;
        _qso.SotaRef = SotaRef;
        _qso.MySigInfo = MySigInfo;
        _qso.SigInfo = SigInfo;
        _qso.StationCallsign = StationCallsign.Trim().ToUpperInvariant();
        _qso.OperatorCallsign = OperatorCallsign;
        _qso.MyGridSquare = MyGridSquare;
        _qso.MyState = MyState;
        _qso.MyCounty = MyCounty;
        _qso.Qth = Qth;
        _qso.Op = Op;
        _qso.UtcOffsetHours = decimal.TryParse(UtcOffsetHoursText, NumberStyles.Number, CultureInfo.InvariantCulture, out var utcOffset) ? utcOffset : null;
        _qso.ObservesDaylightSavingTime = ObservesDaylightSavingTime;

        if (resolvedEntity is not null && !_qso.DxccEntityOverride)
        {
            _qso.DxccEntityCode = resolvedEntity.EntityCode;
            _qso.Country = string.IsNullOrWhiteSpace(_qso.Country) ? resolvedEntity.EntityName : _qso.Country;
            _qso.Continent ??= resolvedEntity.Continent;
        }

        await _qsoRepository.UpdateAsync(_qso);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_qso is null) return;
        await _qsoRepository.DeleteAsync(_qso.Id);
        Deleted?.Invoke(this, EventArgs.Empty);
    }
}
