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
    private readonly DispatcherTimer _catPollTimer;
    private string? _lastLookedUpCallsign;

    public event EventHandler? QsoLogged;
    public event EventHandler<string>? CallsignChanged;

    [ObservableProperty] private string callsign = string.Empty;
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
    [ObservableProperty] private string? comment;
    [ObservableProperty] private bool isLookingUp;
    [ObservableProperty] private StationProfile? selectedStationProfile;
    [ObservableProperty] private bool isCatConnected;
    [ObservableProperty] private bool isCatAutoFillPaused;
    [ObservableProperty] private string? catStatusMessage;
    [ObservableProperty] private string? subMode;

    public ObservableCollection<string> Bands { get; } = new(QsoFieldOptions.Bands);
    public ObservableCollection<string> Modes { get; } = new(QsoFieldOptions.Modes);

    /// <summary>rigctld can only ever report the generic "DATA" bucket for a digital contact (see
    /// DataModeVisibilityConverter), so this is how the operator specifies which digital protocol was
    /// actually used.</summary>
    public ObservableCollection<string> SubModes { get; } = new(QsoFieldOptions.SubModes);

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
        IClock clock)
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

        _catPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _catPollTimer.Tick += OnCatPollTick;
    }

    /// <summary>Lets MainViewModel drive the log grid's search filter from this field, so the operator
    /// sees prior contacts with a station (duplicate check, history) as they type — mirrors how
    /// QsoLogged already drives QsoLog.RefreshAsync from here.</summary>
    partial void OnCallsignChanged(string value) => CallsignChanged?.Invoke(this, value);

    /// <summary>Clears any leftover Sub-Mode selection when Mode is switched away from "DATA", so it
    /// can't silently persist into a SSB/CW/etc QSO that no longer shows the Sub-Mode picker.</summary>
    partial void OnModeChanged(string value)
    {
        if (!string.Equals(value, "DATA", StringComparison.OrdinalIgnoreCase)) SubMode = null;
    }

    public async Task InitializeAsync()
    {
        var profiles = await _stationProfileRepository.GetAllAsync();
        StationProfiles.Clear();
        foreach (var p in profiles) StationProfiles.Add(p);

        SelectedStationProfile = StationProfiles.FirstOrDefault(p => p.Id == _settings.LastUsedStationProfileId)
            ?? StationProfiles.FirstOrDefault(p => p.IsDefault)
            ?? StationProfiles.FirstOrDefault();
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
        // Frequency/Band/Mode are the only fields CAT ever writes — they should keep tracking the
        // live radio state even while the operator is mid-way typing the next contact's callsign.
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
            QsoDateTimeOnUtc = _clock.UtcNow,
            Band = Band,
            Mode = Mode,
            SubMode = string.Equals(Mode, "DATA", StringComparison.OrdinalIgnoreCase) ? SubMode : null,
            FrequencyMhz = decimal.TryParse(FrequencyMhz, out var freq) ? freq : null,
            RstSent = RstSent,
            RstRcvd = RstRcvd,
            Name = Name,
            GridSquare = GridSquare,
            City = City,
            State = State,
            County = County,
            Country = Country,
            ArrlSection = ArrlSection,
            CqZone = int.TryParse(CqZone, out var cqZoneValue) ? cqZoneValue : null,
            ItuZone = int.TryParse(ItuZone, out var ituZoneValue) ? ituZoneValue : null,
            Comment = Comment,
            StationProfileId = SelectedStationProfile.Id,
            StationCallsign = SelectedStationProfile.Callsign,
            OperatorCallsign = SelectedStationProfile.OperatorCallsign,
            MyGridSquare = SelectedStationProfile.MyGridSquare,
            MyState = SelectedStationProfile.MyState,
            MyCounty = SelectedStationProfile.MyCounty,
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

        _settings.LastUsedStationProfileId = SelectedStationProfile.Id;

        QsoLogged?.Invoke(this, EventArgs.Empty);
        ResetForNextQso();
    }

    private void ResetForNextQso()
    {
        _lastLookedUpCallsign = null;
        Callsign = string.Empty;
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
