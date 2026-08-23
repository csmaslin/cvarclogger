using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.ViewModels;

/// <summary>Tracks summits toward SOTA's "Mountain Goat" activator award (1000 lifetime activator
/// points). Points are resolved automatically from the official SOTA summit list, not hand-entered.</summary>
public partial class MountainGoatViewModel : ObservableObject
{
    private readonly ISotaActivationRepository _repository;
    private readonly IQsoRepository _qsoRepository;
    private readonly SotaSummitLookupService _lookupService;
    private readonly DialogService _dialogService;

    public ObservableCollection<SotaActivation> Activations { get; } = new();

    [ObservableProperty] private string newSummitCode = string.Empty;
    [ObservableProperty] private bool isLookingUp;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private int databaseSummitCount;
    [ObservableProperty] private SotaActivation? selectedActivation;
    [ObservableProperty] private SotaSummitInfo? selectedSummitDetails;
    [ObservableProperty] private ObservableCollection<Qso> activationHistory = new();
    [ObservableProperty] private int selectedSummitS2SPoints;

    public int TotalPoints => Activations
        .Where(a => a.Activated && a.ActivationDateUtc.HasValue && a.ActivationDateUtc.Value.Year == DateTime.UtcNow.Year)
        .Sum(a => a.Points);

    private Dictionary<int, SotaSummitInfo> _summitDetails = new();
    private List<Qso> _allQsos = new();

    public int TotalS2SPoints
    {
        get
        {
            // S2S points are earned when user is on a summit (MySotaRef) and contacts another summit (SotaRef)
            // Only count S2S QSOs from the current calendar year, multiply by 2 points per contact
            int s2sCount = _allQsos
                .Where(q => !string.IsNullOrWhiteSpace(q.MySotaRef) &&
                            !string.IsNullOrWhiteSpace(q.SotaRef) &&
                            q.QsoDateTimeOnUtc.Year == DateTime.UtcNow.Year)
                .Count();
            return s2sCount * 2;
        }
    }

    public MountainGoatViewModel(ISotaActivationRepository repository, IQsoRepository qsoRepository, SotaSummitLookupService lookupService, DialogService dialogService)
    {
        _repository = repository;
        _qsoRepository = qsoRepository;
        _lookupService = lookupService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var activations = await _repository.GetAllAsync();
        Activations.Clear();
        foreach (var a in activations) Activations.Add(a);

        _allQsos = await _qsoRepository.GetAllAsync();

        await BackfillMissingSummitNamesAsync();
        await SyncFromQsoLogAsync();
        await UpdateContactCountsAsync();
        await CacheAllSummitDetailsAsync();

        DatabaseSummitCount = await _lookupService.GetSummitCountAsync();

        OnPropertyChanged(nameof(TotalPoints));
        OnPropertyChanged(nameof(TotalS2SPoints));
    }

    /// <summary>Caches summit details (including S2S points) for all activated summits.</summary>
    private async Task CacheAllSummitDetailsAsync()
    {
        foreach (var activation in Activations.Where(a => a.Activated))
        {
            if (_summitDetails.ContainsKey(activation.Id)) continue;

            try
            {
                var details = await _lookupService.LookupAsync(activation.SummitCode);
                if (details is not null)
                {
                    _summitDetails[activation.Id] = details;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                // Best-effort -- if one lookup fails, continue with the rest
            }
        }
    }

    /// <summary>Computes the contact count for each tracked summit from the QSO log.</summary>
    private async Task UpdateContactCountsAsync()
    {
        var qsos = await _qsoRepository.GetAllAsync();
        var countBySummit = qsos
            .Where(q => !string.IsNullOrWhiteSpace(q.MySotaRef))
            .GroupBy(q => SotaSummitLookupService.Normalize(q.MySotaRef!))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var activation in Activations)
        {
            string normalized = SotaSummitLookupService.Normalize(activation.SummitCode);
            activation.ContactCount = countBySummit.TryGetValue(normalized, out var count) ? count : 0;
        }
    }

    /// <summary>Rows added before the SummitName column existed have it blank -- this looks each of
    /// those up from the SOTA summit list and persists it, one time, so real pre-existing production
    /// data (e.g. W6/CC-004) shows a proper name without the user having to re-add it.</summary>
    private async Task BackfillMissingSummitNamesAsync()
    {
        foreach (var activation in Activations.Where(a => string.IsNullOrWhiteSpace(a.SummitName)))
        {
            SotaSummitInfo? info;
            try
            {
                info = await _lookupService.LookupAsync(activation.SummitCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                continue; // best-effort -- a lookup failure here shouldn't block the rest of the window from loading
            }
            if (info is null || string.IsNullOrWhiteSpace(info.SummitName)) continue;

            activation.SummitName = info.SummitName;
            await _repository.UpdateAsync(activation);
        }
    }

    /// <summary>A valid SOTA activation requires at least 4 contacts logged from the summit on the
    /// same UTC calendar date (the "My SOTA" field on the entry form) -- fewer than that doesn't earn
    /// points. For each summit that's had any such contact, this always refreshes the tracked row's
    /// date/time to the first contact of whichever date actually cleared the 4-contact bar (or, if
    /// none has yet, the first contact attempted, so progress is still visible), adding it to the
    /// tracked list first via a summit lookup if it isn't there yet. Contacts logged with slightly
    /// different formatting (e.g. a missing hyphen) are normalized so they still count toward the
    /// same summit. Never overwrites a row that's already Activated -- that's either a qualified,
    /// locked-in activation or a hand-edited manual entry, and shouldn't be clobbered either way.</summary>
    private async Task SyncFromQsoLogAsync()
    {
        const int MinContactsToActivate = 4;

        var qsos = await _qsoRepository.GetAllAsync();
        var bySummit = qsos
            .Where(q => !string.IsNullOrWhiteSpace(q.MySotaRef))
            .GroupBy(q => SotaSummitLookupService.Normalize(q.MySotaRef!));

        foreach (var summitGroup in bySummit)
        {
            var qualifyingDate = summitGroup
                .GroupBy(q => q.QsoDateTimeOnUtc.Date)
                .Where(g => g.Count() >= MinContactsToActivate)
                .OrderBy(g => g.Key)
                .FirstOrDefault();

            bool qualifies = qualifyingDate is not null;
            DateTime firstContactUtc = qualifyingDate?.Min(q => q.QsoDateTimeOnUtc)
                ?? summitGroup.Min(q => q.QsoDateTimeOnUtc);

            string rawCode = summitGroup.First().MySotaRef!.Trim();
            var existing = Activations.FirstOrDefault(a => SotaSummitLookupService.Normalize(a.SummitCode) == SotaSummitLookupService.Normalize(rawCode));

            if (existing is not null)
            {
                if (existing.Activated) continue; // already qualified (or hand-set) -- don't clobber it
                existing.Activated = qualifies;
                existing.ActivationDateUtc = firstContactUtc;
                await _repository.UpdateAsync(existing);
                continue;
            }

            SotaSummitInfo? info;
            try
            {
                info = await _lookupService.LookupAsync(rawCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                continue; // best-effort -- a lookup failure here shouldn't block the rest of the window from loading
            }
            if (info is null) continue;

            var newActivation = new SotaActivation
            {
                SummitCode = info.SummitCode,
                SummitName = info.SummitName,
                Points = info.Points,
                Activated = qualifies,
                ActivationDateUtc = firstContactUtc,
            };
            await _repository.AddAsync(newActivation);
            Activations.Add(newActivation);
        }
    }

    /// <summary>Force-downloads the SOTA summit list regardless of its cache age, then re-backfills
    /// and re-syncs from it -- unlike the automatic 30-day staleness check, this always hits the
    /// network, for the user-triggered "Refresh Summit List" button.</summary>
    [RelayCommand]
    private async Task RefreshListAsync()
    {
        IsRefreshing = true;
        try
        {
            await _lookupService.RefreshAsync();
            await BackfillMissingSummitNamesAsync();
            await SyncFromQsoLogAsync();
            OnPropertyChanged(nameof(TotalPoints));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _dialogService.ShowError($"Could not refresh the SOTA summit list: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddSummitAsync()
    {
        string code = NewSummitCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            _dialogService.ShowError("Enter a SOTA summit code (e.g. W6/CT-003).");
            return;
        }

        if (Activations.Any(a => a.SummitCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            _dialogService.ShowError($"{code} is already being tracked.");
            return;
        }

        IsLookingUp = true;
        try
        {
            var info = await _lookupService.LookupAsync(code);
            if (info is null)
            {
                _dialogService.ShowError($"'{code}' was not found in the SOTA summit list.");
                return;
            }

            var activation = new SotaActivation
            {
                SummitCode = info.SummitCode,
                SummitName = info.SummitName,
                Points = info.Points,
                Activated = false,
            };
            await _repository.AddAsync(activation);
            Activations.Add(activation);
            OnPropertyChanged(nameof(TotalPoints));
            NewSummitCode = string.Empty;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _dialogService.ShowError($"Could not look up the SOTA summit list: {ex.Message}");
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(SotaActivation? item)
    {
        if (item is null) return;
        if (!_dialogService.Confirm($"Stop tracking {item.SummitCode}?")) return;
        await _repository.DeleteAsync(item.Id);
        Activations.Remove(item);
        OnPropertyChanged(nameof(TotalPoints));
    }

    /// <summary>Persists an in-place edit (Activated checkbox toggle or date/time edit) made
    /// directly on a grid row, since SotaActivation is a plain model and isn't itself observable.</summary>
    public async Task SaveRowAsync(SotaActivation item)
    {
        await _repository.UpdateAsync(item);
        OnPropertyChanged(nameof(TotalPoints));
    }

    partial void OnSelectedActivationChanged(SotaActivation? value) => _ = SelectActivationAsync(value);

    /// <summary>Looks up and displays details for the selected summit, including activation history and S2S points.</summary>
    private async Task SelectActivationAsync(SotaActivation? activation)
    {
        if (activation is null)
        {
            SelectedSummitDetails = null;
            ActivationHistory.Clear();
            SelectedSummitS2SPoints = 0;
            return;
        }

        try
        {
            var details = await _lookupService.LookupAsync(activation.SummitCode);
            SelectedSummitDetails = details;

            if (details is not null)
            {
                _summitDetails[activation.Id] = details;
                OnPropertyChanged(nameof(TotalS2SPoints));
            }

            string normalized = SotaSummitLookupService.Normalize(activation.SummitCode);
            var summitQsos = _allQsos
                .Where(q => !string.IsNullOrWhiteSpace(q.MySotaRef) &&
                            SotaSummitLookupService.Normalize(q.MySotaRef!) == normalized)
                .OrderByDescending(q => q.QsoDateTimeOnUtc)
                .ToList();

            ActivationHistory.Clear();
            foreach (var qso in summitQsos)
            {
                ActivationHistory.Add(qso);
            }

            // Calculate S2S points for this summit (2 points per S2S contact)
            int s2sContactCount = summitQsos
                .Where(q => !string.IsNullOrWhiteSpace(q.SotaRef))
                .Count();
            SelectedSummitS2SPoints = s2sContactCount * 2;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            SelectedSummitDetails = null;
            ActivationHistory.Clear();
            SelectedSummitS2SPoints = 0;
        }
    }
}
