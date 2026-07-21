using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcCellLog.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.ViewModels;

/// <summary>Tracks parks toward POTA's activator award tiers (Bronze 10, Silver 20, Gold 30,
/// Platinum 40, Diamond 50, Ruby 100, Emerald 125 unique parks activated) and the separate per-park
/// Kilo award (1,000 cumulative QSOs at one park). Park names are resolved automatically from the
/// bundled POTA park list, not hand-entered. Ported from the WPF app's identically-named ViewModel --
/// same sync/activation logic, adapted to this app's ErrorMessage-property pattern instead of a
/// DialogService.</summary>
public partial class ParksOnTheAirViewModel : ObservableObject
{
    private readonly IPotaActivationRepository _repository;
    private readonly IQsoRepository _qsoRepository;
    private readonly PotaParkLookupService _lookupService;

    public ObservableCollection<PotaActivation> Activations { get; } = new();

    [ObservableProperty] private string newParkReference = string.Empty;
    [ObservableProperty] private bool isLookingUp;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    public int TotalParksActivated => Activations.Count(a => a.Activated);
    public int KiloParkCount => Activations.Count(a => a.IsKiloEligible);
    public string CurrentAwardTier => ComputeAwardTier(TotalParksActivated);

    public ParksOnTheAirViewModel(IPotaActivationRepository repository, IQsoRepository qsoRepository, PotaParkLookupService lookupService)
    {
        _repository = repository;
        _qsoRepository = qsoRepository;
        _lookupService = lookupService;
    }

    private static string ComputeAwardTier(int parksActivated) => parksActivated switch
    {
        >= 125 => "Emerald",
        >= 100 => "Ruby",
        >= 50 => "Diamond",
        >= 40 => "Platinum",
        >= 30 => "Gold",
        >= 20 => "Silver",
        >= 10 => "Bronze",
        _ => "None",
    };

    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalParksActivated));
        OnPropertyChanged(nameof(KiloParkCount));
        OnPropertyChanged(nameof(CurrentAwardTier));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var activations = await _repository.GetAllAsync();
        Activations.Clear();
        foreach (var a in activations) Activations.Add(a);

        await BackfillMissingParkNamesAsync();
        await SyncFromQsoLogAsync();

        NotifyTotalsChanged();
    }

    /// <summary>Rows whose ParkName didn't resolve at add-time (e.g. a lookup failure) have it blank --
    /// this retries the lookup and persists it, one time, so the list doesn't keep showing a blank name
    /// forever once the bundled park list actually has the entry.</summary>
    private async Task BackfillMissingParkNamesAsync()
    {
        foreach (var activation in Activations.Where(a => string.IsNullOrWhiteSpace(a.ParkName)))
        {
            PotaParkInfo? info;
            try
            {
                info = await _lookupService.LookupAsync(activation.ParkReference);
            }
            catch (IOException)
            {
                continue; // best-effort -- a lookup failure here shouldn't block the rest of the page from loading
            }
            if (info is null || string.IsNullOrWhiteSpace(info.Name)) continue;

            activation.ParkName = info.Name;
            await _repository.UpdateAsync(activation);
        }
    }

    /// <summary>A valid POTA activation requires at least 10 contacts with 10 different callsigns
    /// logged from the park on the same UTC calendar date (the "My POTA" field on the entry form) --
    /// working the same station repeatedly doesn't add toward the 10. For each park that's had any
    /// such contact, this refreshes the tracked row's cumulative QSO count (used for the separate Kilo
    /// award) every time, but only sets Activated/ActivationDateUtc the first time the 10-unique-
    /// callsign bar is actually cleared -- once Activated, that date is locked in and isn't overwritten
    /// by later re-syncs, even though the QSO count keeps climbing toward Kilo. Contacts logged with
    /// slightly different formatting (e.g. a missing hyphen) are normalized so they still count toward
    /// the same park.</summary>
    private async Task SyncFromQsoLogAsync()
    {
        const int MinUniqueCallsignsToActivate = 10;

        var qsos = await _qsoRepository.GetAllAsync();
        var byPark = qsos
            .Where(q => !string.IsNullOrWhiteSpace(q.MySigInfo))
            .GroupBy(q => PotaParkLookupService.Normalize(q.MySigInfo!));

        foreach (var parkGroup in byPark)
        {
            int totalQsoCount = parkGroup.Count();

            var qualifyingDate = parkGroup
                .GroupBy(q => q.QsoDateTimeOnUtc.Date)
                .Where(g => g.Select(q => q.Callsign.Trim().ToUpperInvariant()).Distinct().Count() >= MinUniqueCallsignsToActivate)
                .OrderBy(g => g.Key)
                .FirstOrDefault();

            bool qualifies = qualifyingDate is not null;
            DateTime firstContactUtc = qualifyingDate?.Min(q => q.QsoDateTimeOnUtc)
                ?? parkGroup.Min(q => q.QsoDateTimeOnUtc);

            string rawRef = parkGroup.First().MySigInfo!.Trim();
            var existing = Activations.FirstOrDefault(a => PotaParkLookupService.Normalize(a.ParkReference) == PotaParkLookupService.Normalize(rawRef));

            if (existing is not null)
            {
                existing.TotalQsoCount = totalQsoCount;
                if (!existing.Activated)
                {
                    existing.Activated = qualifies;
                    existing.ActivationDateUtc = firstContactUtc;
                }
                await _repository.UpdateAsync(existing);
                continue;
            }

            PotaParkInfo? info;
            try
            {
                info = await _lookupService.LookupAsync(rawRef);
            }
            catch (IOException)
            {
                continue; // best-effort -- a lookup failure here shouldn't block the rest of the page from loading
            }
            if (info is null) continue;

            var newActivation = new PotaActivation
            {
                ParkReference = info.Reference,
                ParkName = info.Name,
                Activated = qualifies,
                ActivationDateUtc = firstContactUtc,
                TotalQsoCount = totalQsoCount,
            };
            await _repository.AddAsync(newActivation);
            Activations.Add(newActivation);
        }
    }

    /// <summary>Re-queries live park info (name) for every park currently being tracked. POTA has no
    /// bulk "all parks" export to refresh a local snapshot from, so unlike SOTA's refresh this doesn't
    /// rebuild the bundled list -- it re-fetches only the parks you actually care about via the live
    /// single-park API.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        ErrorMessage = null;
        try
        {
            foreach (var activation in Activations.ToList())
            {
                var info = await _lookupService.LookupAsync(activation.ParkReference);
                if (info is null || string.Equals(info.Name, activation.ParkName, StringComparison.Ordinal)) continue;

                activation.ParkName = info.Name;
                await _repository.UpdateAsync(activation);
            }
        }
        catch (IOException ex)
        {
            ErrorMessage = $"Could not refresh park info: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddParkAsync()
    {
        ErrorMessage = null;
        string reference = NewParkReference.Trim();
        if (string.IsNullOrWhiteSpace(reference))
        {
            ErrorMessage = "Enter a POTA park reference (e.g. US-0001).";
            return;
        }

        if (Activations.Any(a => PotaParkLookupService.Normalize(a.ParkReference) == PotaParkLookupService.Normalize(reference)))
        {
            ErrorMessage = $"{reference} is already being tracked.";
            return;
        }

        IsLookingUp = true;
        try
        {
            var info = await _lookupService.LookupAsync(reference);
            if (info is null)
            {
                ErrorMessage = $"'{reference}' was not found in the POTA park list.";
                return;
            }

            var activation = new PotaActivation
            {
                ParkReference = info.Reference,
                ParkName = info.Name,
                Activated = false,
            };
            await _repository.AddAsync(activation);
            Activations.Add(activation);
            NotifyTotalsChanged();
            NewParkReference = string.Empty;
        }
        catch (IOException ex)
        {
            ErrorMessage = $"Could not look up the POTA park list: {ex.Message}";
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    /// <summary>Called from AwardsPage's code-behind after the user confirms via DisplayAlert --
    /// matches StationProfilesPage's delete-confirmation pattern.</summary>
    public async Task DeleteAsync(PotaActivation item)
    {
        await _repository.DeleteAsync(item.Id);
        Activations.Remove(item);
        NotifyTotalsChanged();
    }
}
