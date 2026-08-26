using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.ViewModels;

/// <summary>Backs the Bulk Edit form (see QsoLogGridView's "Bulk Edit" button). Every field starts blank
/// -- unlike QsoEditViewModel, there is no single QSO to seed values from, since the operator is editing
/// several at once. On Save, only fields the operator actually typed/selected something into are applied,
/// and each of those overwrites that one field on every QSO in the selection; every other field on every
/// QSO is left exactly as it was. The three-value ComboBoxes (QSL/LoTW status, Observes DST) carry an
/// explicit "(No Change)" option for the same reason a blank TextBox means "leave this field alone" --
/// there's no natural "blank" for an enum or a bool.</summary>
public partial class BulkEditViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly DialogService _dialogService;
    private readonly SkccRefDatabase _skccRefDatabase;
    private IReadOnlyList<Qso> _qsos = Array.Empty<Qso>();

    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";
    private const string DateFormat = "yyyy-MM-dd";
    public const string NoChangeOption = "(No Change)";

    public event EventHandler? Saved;

    [ObservableProperty] private int qsoCount;
    [ObservableProperty] private bool isLookingUpSkcc;

    [ObservableProperty] private string? qsoDateTimeUtcText;
    [ObservableProperty] private string? qsoDateTimeOffUtcText;
    [ObservableProperty] private string? callsign;
    [ObservableProperty] private string? band;
    [ObservableProperty] private string? mode;
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
    [ObservableProperty] private string qslSentOption = NoChangeOption;
    [ObservableProperty] private string qslRcvdOption = NoChangeOption;
    [ObservableProperty] private string? qslSentDateText;
    [ObservableProperty] private string? qslRcvdDateText;
    [ObservableProperty] private string lotwQslSentOption = NoChangeOption;
    [ObservableProperty] private string lotwQslRcvdOption = NoChangeOption;
    [ObservableProperty] private string? lotwQslSentDateText;
    [ObservableProperty] private string? lotwQslRcvdDateText;
    [ObservableProperty] private string? qslViaCallsign;
    [ObservableProperty] private string? stationCallsign;
    [ObservableProperty] private string? operatorCallsign;
    [ObservableProperty] private string? myGridSquare;
    [ObservableProperty] private string? myState;
    [ObservableProperty] private string? myCounty;
    [ObservableProperty] private string? qth;
    [ObservableProperty] private string? op;
    [ObservableProperty] private string? utcOffsetHours;
    [ObservableProperty] private string observesDaylightSavingTimeOption = NoChangeOption;
    [ObservableProperty] private string? precedence;
    [ObservableProperty] private string? check;
    [ObservableProperty] private string? @class;
    [ObservableProperty] private string? stxSerial;
    [ObservableProperty] private string? skccNr;
    [ObservableProperty] private string? mySkccNr;

    public ObservableCollection<string> Bands { get; } = new(new[] { "" }.Concat(QsoFieldOptions.Bands));
    public ObservableCollection<string> Modes { get; } = new(new[] { "" }.Concat(QsoFieldOptions.Modes));
    public ObservableCollection<string> SubModes { get; } =
        new(new[] { "" }.Concat(QsoFieldOptions.PskSubModes).Concat(QsoFieldOptions.DigitalVoiceSubModes).Concat(QsoFieldOptions.SsbSubModes).Distinct());

    public ObservableCollection<string> QslStatusOptions { get; } = new(new[] { NoChangeOption }.Concat(Enum.GetNames<QslStatus>()));
    public ObservableCollection<string> YesNoOptions { get; } = new(new[] { NoChangeOption, "Yes", "No" });

    public BulkEditViewModel(IQsoRepository qsoRepository, ICallsignEntityResolver entityResolver, DialogService dialogService, SkccRefDatabase skccRefDatabase)
    {
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
        _dialogService = dialogService;
        _skccRefDatabase = skccRefDatabase;
    }

    public void Load(IReadOnlyList<Qso> qsos)
    {
        _qsos = qsos;
        QsoCount = qsos.Count;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_qsos.Count == 0) return;

        bool anyChange =
            !string.IsNullOrWhiteSpace(QsoDateTimeUtcText) || !string.IsNullOrWhiteSpace(QsoDateTimeOffUtcText) || !string.IsNullOrWhiteSpace(Callsign)
            || !string.IsNullOrWhiteSpace(Band) || !string.IsNullOrWhiteSpace(Mode) || !string.IsNullOrWhiteSpace(SubMode)
            || !string.IsNullOrWhiteSpace(FrequencyMhz) || !string.IsNullOrWhiteSpace(FrequencyRxMhz)
            || !string.IsNullOrWhiteSpace(RstSent) || !string.IsNullOrWhiteSpace(RstRcvd) || !string.IsNullOrWhiteSpace(Name)
            || !string.IsNullOrWhiteSpace(GridSquare) || !string.IsNullOrWhiteSpace(City) || !string.IsNullOrWhiteSpace(State)
            || !string.IsNullOrWhiteSpace(County) || !string.IsNullOrWhiteSpace(Country) || !string.IsNullOrWhiteSpace(ArrlSection)
            || !string.IsNullOrWhiteSpace(Continent) || !string.IsNullOrWhiteSpace(CqZone) || !string.IsNullOrWhiteSpace(ItuZone)
            || !string.IsNullOrWhiteSpace(MySotaRef) || !string.IsNullOrWhiteSpace(SotaRef) || !string.IsNullOrWhiteSpace(MySigInfo)
            || !string.IsNullOrWhiteSpace(SigInfo) || !string.IsNullOrWhiteSpace(TxPowerWatts) || !string.IsNullOrWhiteSpace(Comment)
            || QslSentOption != NoChangeOption || QslRcvdOption != NoChangeOption
            || !string.IsNullOrWhiteSpace(QslSentDateText) || !string.IsNullOrWhiteSpace(QslRcvdDateText)
            || LotwQslSentOption != NoChangeOption || LotwQslRcvdOption != NoChangeOption
            || !string.IsNullOrWhiteSpace(LotwQslSentDateText) || !string.IsNullOrWhiteSpace(LotwQslRcvdDateText)
            || !string.IsNullOrWhiteSpace(QslViaCallsign) || !string.IsNullOrWhiteSpace(StationCallsign)
            || !string.IsNullOrWhiteSpace(OperatorCallsign) || !string.IsNullOrWhiteSpace(MyGridSquare)
            || !string.IsNullOrWhiteSpace(MyState) || !string.IsNullOrWhiteSpace(MyCounty) || !string.IsNullOrWhiteSpace(Qth)
            || !string.IsNullOrWhiteSpace(Op) || !string.IsNullOrWhiteSpace(UtcOffsetHours)
            || ObservesDaylightSavingTimeOption != NoChangeOption
            || !string.IsNullOrWhiteSpace(Precedence) || !string.IsNullOrWhiteSpace(Check) || !string.IsNullOrWhiteSpace(Class)
            || !string.IsNullOrWhiteSpace(StxSerial) || !string.IsNullOrWhiteSpace(SkccNr) || !string.IsNullOrWhiteSpace(MySkccNr);

        if (!anyChange)
        {
            _dialogService.ShowError("Enter a value in at least one field before saving.");
            return;
        }

        if (!_dialogService.Confirm($"Apply the entered field(s) to all {_qsos.Count} selected QSOs? Blank fields are left unchanged."))
            return;

        bool callsignChanged = !string.IsNullOrWhiteSpace(Callsign);

        foreach (var qso in _qsos)
        {
            if (!string.IsNullOrWhiteSpace(QsoDateTimeUtcText)
                && DateTime.TryParseExact(QsoDateTimeUtcText, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var qsoDateTime))
                qso.QsoDateTimeOnUtc = DateTime.SpecifyKind(qsoDateTime, DateTimeKind.Utc);
            if (!string.IsNullOrWhiteSpace(QsoDateTimeOffUtcText)
                && DateTime.TryParseExact(QsoDateTimeOffUtcText, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var qsoOffDateTime))
                qso.QsoDateTimeOffUtc = DateTime.SpecifyKind(qsoOffDateTime, DateTimeKind.Utc);

            if (callsignChanged) qso.Callsign = Callsign!.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(Band)) qso.Band = Band;
            if (!string.IsNullOrWhiteSpace(Mode)) qso.Mode = Mode;
            if (!string.IsNullOrWhiteSpace(SubMode)) qso.SubMode = SubMode;
            if (!string.IsNullOrWhiteSpace(FrequencyMhz) && decimal.TryParse(FrequencyMhz, NumberStyles.Number, CultureInfo.InvariantCulture, out var freq))
                qso.FrequencyMhz = freq;
            if (!string.IsNullOrWhiteSpace(FrequencyRxMhz) && decimal.TryParse(FrequencyRxMhz, NumberStyles.Number, CultureInfo.InvariantCulture, out var freqRx))
                qso.FrequencyRxMhz = freqRx;
            if (!string.IsNullOrWhiteSpace(RstSent)) qso.RstSent = RstSent;
            if (!string.IsNullOrWhiteSpace(RstRcvd)) qso.RstRcvd = RstRcvd;
            if (!string.IsNullOrWhiteSpace(Name)) qso.Name = Name;
            if (!string.IsNullOrWhiteSpace(GridSquare)) qso.GridSquare = GridSquare;
            if (!string.IsNullOrWhiteSpace(City)) qso.City = City;
            if (!string.IsNullOrWhiteSpace(State)) qso.State = State.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(County)) qso.County = County;
            if (!string.IsNullOrWhiteSpace(Country)) qso.Country = Country;
            if (!string.IsNullOrWhiteSpace(ArrlSection)) qso.ArrlSection = ArrlSection.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(Continent)) qso.Continent = Continent;
            if (!string.IsNullOrWhiteSpace(CqZone) && int.TryParse(CqZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cqZoneValue))
                qso.CqZone = cqZoneValue;
            if (!string.IsNullOrWhiteSpace(ItuZone) && int.TryParse(ItuZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ituZoneValue))
                qso.ItuZone = ituZoneValue;
            if (!string.IsNullOrWhiteSpace(MySotaRef)) qso.MySotaRef = MySotaRef;
            if (!string.IsNullOrWhiteSpace(SotaRef)) qso.SotaRef = SotaRef;
            if (!string.IsNullOrWhiteSpace(MySigInfo)) qso.MySigInfo = MySigInfo;
            if (!string.IsNullOrWhiteSpace(SigInfo)) qso.SigInfo = SigInfo;
            if (!string.IsNullOrWhiteSpace(TxPowerWatts) && decimal.TryParse(TxPowerWatts, NumberStyles.Number, CultureInfo.InvariantCulture, out var txPower))
                qso.TxPowerWatts = txPower;
            if (!string.IsNullOrWhiteSpace(Comment)) qso.Comment = Comment;

            if (QslSentOption != NoChangeOption && Enum.TryParse<QslStatus>(QslSentOption, out var qslSent)) qso.QslSent = qslSent;
            if (QslRcvdOption != NoChangeOption && Enum.TryParse<QslStatus>(QslRcvdOption, out var qslRcvd)) qso.QslRcvd = qslRcvd;
            if (!string.IsNullOrWhiteSpace(QslSentDateText) && DateTime.TryParseExact(QslSentDateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var qslSentDate))
                qso.QslSentDate = qslSentDate;
            if (!string.IsNullOrWhiteSpace(QslRcvdDateText) && DateTime.TryParseExact(QslRcvdDateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var qslRcvdDate))
                qso.QslRcvdDate = qslRcvdDate;

            if (LotwQslSentOption != NoChangeOption && Enum.TryParse<QslStatus>(LotwQslSentOption, out var lotwSent)) qso.LotwQslSent = lotwSent;
            if (LotwQslRcvdOption != NoChangeOption && Enum.TryParse<QslStatus>(LotwQslRcvdOption, out var lotwRcvd)) qso.LotwQslRcvd = lotwRcvd;
            if (!string.IsNullOrWhiteSpace(LotwQslSentDateText) && DateTime.TryParseExact(LotwQslSentDateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lotwSentDate))
                qso.LotwQslSentDate = lotwSentDate;
            if (!string.IsNullOrWhiteSpace(LotwQslRcvdDateText) && DateTime.TryParseExact(LotwQslRcvdDateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lotwRcvdDate))
                qso.LotwQslRcvdDate = lotwRcvdDate;

            if (!string.IsNullOrWhiteSpace(QslViaCallsign)) qso.QslViaCallsign = QslViaCallsign;
            if (!string.IsNullOrWhiteSpace(StationCallsign)) qso.StationCallsign = StationCallsign.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(OperatorCallsign)) qso.OperatorCallsign = OperatorCallsign;
            if (!string.IsNullOrWhiteSpace(MyGridSquare)) qso.MyGridSquare = MyGridSquare;
            if (!string.IsNullOrWhiteSpace(MyState)) qso.MyState = MyState;
            if (!string.IsNullOrWhiteSpace(MyCounty)) qso.MyCounty = MyCounty;
            if (!string.IsNullOrWhiteSpace(Qth)) qso.Qth = Qth;
            if (!string.IsNullOrWhiteSpace(Op)) qso.Op = Op;
            if (!string.IsNullOrWhiteSpace(UtcOffsetHours) && decimal.TryParse(UtcOffsetHours, NumberStyles.Number, CultureInfo.InvariantCulture, out var utcOffset))
                qso.UtcOffsetHours = utcOffset;
            if (ObservesDaylightSavingTimeOption != NoChangeOption)
                qso.ObservesDaylightSavingTime = ObservesDaylightSavingTimeOption == "Yes";

            if (!string.IsNullOrWhiteSpace(Precedence)) qso.Precedence = Precedence;
            if (!string.IsNullOrWhiteSpace(Check)) qso.Check = Check;
            if (!string.IsNullOrWhiteSpace(Class)) qso.Class = Class;
            if (!string.IsNullOrWhiteSpace(StxSerial) && int.TryParse(StxSerial, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stxSerial))
                qso.StxSerial = stxSerial;
            if (!string.IsNullOrWhiteSpace(SkccNr)) qso.SkccNr = SkccNr.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(MySkccNr)) qso.MySkccNr = MySkccNr.Trim().ToUpperInvariant();

            if (callsignChanged && !qso.DxccEntityOverride)
            {
                var resolvedEntity = await _entityResolver.ResolveAsync(qso.Callsign);
                if (resolvedEntity is not null) qso.DxccEntityCode = resolvedEntity.EntityCode;
            }

            await _qsoRepository.UpdateAsync(qso);
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Unlike the SKCC # box above (one typed value applied to every selected QSO), this looks
    /// up each selected QSO's own callsign against the SKCC roster individually and fills in that QSO's
    /// own SkccNr. Saves immediately per-QSO rather than waiting for the main Save button, since this is
    /// its own self-contained action distinct from the rest of the bulk-edit form.</summary>
    [RelayCommand]
    private async Task SkccBulkLookupAsync()
    {
        if (_qsos.Count == 0) return;

        if (!_skccRefDatabase.IsAvailable)
        {
            _dialogService.ShowInfo("The SKCC member roster hasn't been downloaded yet. Update it from the SKCC awards tab first.");
            return;
        }

        try
        {
            IsLookingUpSkcc = true;

            var distinctCalls = _qsos.Select(q => q.Callsign).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase);
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var call in distinctCalls)
            {
                var result = await _skccRefDatabase.LookupByNameAsync(call);
                if (result is not null) found[call] = result.Reference;
            }

            int updated = 0;
            foreach (var qso in _qsos)
            {
                if (!found.TryGetValue(qso.Callsign, out var skccNr)) continue;
                qso.SkccNr = skccNr;
                await _qsoRepository.UpdateAsync(qso);
                updated++;
            }

            _dialogService.ShowInfo($"Found SKCC numbers for {updated} of {_qsos.Count} selected QSO(s).");
            if (updated > 0) Saved?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLookingUpSkcc = false;
        }
    }
}
