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
    private Qso? _qso;

    public event EventHandler? Saved;

    [ObservableProperty] private string qsoDateTimeUtcText = string.Empty;
    [ObservableProperty] private string callsign = string.Empty;
    [ObservableProperty] private string band = string.Empty;
    [ObservableProperty] private string mode = string.Empty;
    [ObservableProperty] private string? subMode;
    [ObservableProperty] private string? frequencyMhz;
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
    [ObservableProperty] private QslStatus qslSent;
    [ObservableProperty] private QslStatus qslRcvd;
    [ObservableProperty] private QslStatus lotwQslSent;
    [ObservableProperty] private QslStatus lotwQslRcvd;
    [ObservableProperty] private bool isLookingUp;

    public ObservableCollection<string> Bands { get; } = new(QsoFieldOptions.Bands);
    public ObservableCollection<string> Modes { get; } = new(QsoFieldOptions.Modes);

    /// <summary>Choices for the Sub-Mode picker, shown only while Mode is "DATA" — see
    /// DataModeVisibilityConverter for the full rationale.</summary>
    public ObservableCollection<string> SubModes { get; } = new(QsoFieldOptions.SubModes);

    public Array QslStatuses { get; } = Enum.GetValues(typeof(QslStatus));

    public QsoEditViewModel(
        IQsoRepository qsoRepository,
        LookupCoordinator lookupCoordinator,
        ICallsignEntityResolver entityResolver,
        IGridZoneResolver gridZoneResolver,
        DialogService dialogService)
    {
        _qsoRepository = qsoRepository;
        _lookupCoordinator = lookupCoordinator;
        _entityResolver = entityResolver;
        _gridZoneResolver = gridZoneResolver;
        _dialogService = dialogService;
    }

    public void Load(Qso qso)
    {
        _qso = qso;
        QsoDateTimeUtcText = qso.QsoDateTimeOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        Callsign = qso.Callsign;
        Band = qso.Band;
        Mode = qso.Mode;
        SubMode = qso.SubMode;
        FrequencyMhz = qso.FrequencyMhz?.ToString("0.######", CultureInfo.InvariantCulture);
        RstSent = qso.RstSent;
        RstRcvd = qso.RstRcvd;
        Name = qso.Name;
        GridSquare = qso.GridSquare;
        City = qso.City;
        State = qso.State;
        County = qso.County;
        Country = qso.Country;
        ArrlSection = qso.ArrlSection;
        CqZone = qso.CqZone?.ToString(CultureInfo.InvariantCulture);
        ItuZone = qso.ItuZone?.ToString(CultureInfo.InvariantCulture);
        Comment = qso.Comment;
        QslSent = qso.QslSent;
        QslRcvd = qso.QslRcvd;
        LotwQslSent = qso.LotwQslSent;
        LotwQslRcvd = qso.LotwQslRcvd;
    }

    /// <summary>Clears any leftover Sub-Mode selection when Mode is switched away from "DATA", so it
    /// can't silently persist into a SSB/CW/etc QSO that no longer shows the Sub-Mode picker.</summary>
    partial void OnModeChanged(string value)
    {
        if (!string.Equals(value, "DATA", StringComparison.OrdinalIgnoreCase)) SubMode = null;
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

        if (!DateTime.TryParseExact(QsoDateTimeUtcText, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var qsoDateTime))
        {
            _dialogService.ShowError("Date/Time (UTC) must be in the format yyyy-MM-dd HH:mm.");
            return;
        }

        _qso.QsoDateTimeOnUtc = DateTime.SpecifyKind(qsoDateTime, DateTimeKind.Utc);
        _qso.Callsign = Callsign.Trim().ToUpperInvariant();
        _qso.Band = Band;
        _qso.Mode = Mode;
        _qso.SubMode = string.Equals(Mode, "DATA", StringComparison.OrdinalIgnoreCase) ? SubMode : null;
        _qso.FrequencyMhz = decimal.TryParse(FrequencyMhz, NumberStyles.Number, CultureInfo.InvariantCulture, out var freq) ? freq : null;
        _qso.RstSent = RstSent;
        _qso.RstRcvd = RstRcvd;
        _qso.Name = Name;
        _qso.GridSquare = GridSquare;
        _qso.City = City;
        _qso.State = State;
        _qso.County = County;
        _qso.Country = Country;
        _qso.ArrlSection = ArrlSection;
        _qso.CqZone = int.TryParse(CqZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cqZoneValue) ? cqZoneValue : null;
        _qso.ItuZone = int.TryParse(ItuZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ituZoneValue) ? ituZoneValue : null;
        _qso.Comment = Comment;
        _qso.QslSent = QslSent;
        _qso.QslRcvd = QslRcvd;
        _qso.LotwQslSent = LotwQslSent;
        _qso.LotwQslRcvd = LotwQslRcvd;

        await _qsoRepository.UpdateAsync(_qso);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
