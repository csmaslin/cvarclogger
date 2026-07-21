using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcCellLog.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.ViewModels;

/// <summary>Tracks summits toward SOTA's "Mountain Goat" activator award (1000 lifetime activator
/// points). Points are resolved automatically from the official SOTA summit list, not hand-entered.
/// Ported from the WPF app's identically-named ViewModel -- same sync/activation logic, adapted to
/// this app's ErrorMessage-property pattern instead of a DialogService (there isn't one here; see
/// AwardsPage's code-behind for delete confirmation, matching StationProfilesPage's pattern).</summary>
public partial class MountainGoatViewModel : ObservableObject
{
    private readonly ISotaActivationRepository _repository;
    private readonly IQsoRepository _qsoRepository;
    private readonly SotaSummitLookupService _lookupService;

    public ObservableCollection<SotaActivation> Activations { get; } = new();

    [ObservableProperty] private string newSummitCode = string.Empty;
    [ObservableProperty] private bool isLookingUp;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    public int TotalPoints => Activations.Where(a => a.Activated).Sum(a => a.Points);

    public MountainGoatViewModel(ISotaActivationRepository repository, IQsoRepository qsoRepository, SotaSummitLookupService lookupService)
    {
        _repository = repository;
        _qsoRepository = qsoRepository;
        _lookupService = lookupService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var activations = await _repository.GetAllAsync();
        Activations.Clear();
        foreach (var a in activations) Activations.Add(a);

        await BackfillMissingSummitNamesAsync();
        await SyncFromQsoLogAsync();

        OnPropertyChanged(nameof(TotalPoints));
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
                continue; // best-effort -- a lookup failure here shouldn't block the rest of the page from loading
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
                continue; // best-effort -- a lookup failure here shouldn't block the rest of the page from loading
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
        ErrorMessage = null;
        try
        {
            await _lookupService.RefreshAsync();
            await BackfillMissingSummitNamesAsync();
            await SyncFromQsoLogAsync();
            OnPropertyChanged(nameof(TotalPoints));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            ErrorMessage = $"Could not refresh the SOTA summit list: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddSummitAsync()
    {
        ErrorMessage = null;
        string code = NewSummitCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorMessage = "Enter a SOTA summit code (e.g. W6/CT-003).";
            return;
        }

        if (Activations.Any(a => a.SummitCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            ErrorMessage = $"{code} is already being tracked.";
            return;
        }

        IsLookingUp = true;
        try
        {
            var info = await _lookupService.LookupAsync(code);
            if (info is null)
            {
                ErrorMessage = $"'{code}' was not found in the SOTA summit list.";
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
            ErrorMessage = $"Could not look up the SOTA summit list: {ex.Message}";
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    /// <summary>Called from AwardsPage's code-behind after the user confirms via DisplayAlert --
    /// matches StationProfilesPage's delete-confirmation pattern.</summary>
    public async Task DeleteAsync(SotaActivation item)
    {
        await _repository.DeleteAsync(item.Id);
        Activations.Remove(item);
        OnPropertyChanged(nameof(TotalPoints));
    }
}
