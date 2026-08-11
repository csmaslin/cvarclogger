using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;

namespace CvarcLogger.App.ViewModels;

/// <summary>One unique SKCC member counted toward the operator's own award progress -- the row shown in
/// the Awards Progress "SKCC" tab's grid. QsoDateUtc is the date of the QSO that first made this member
/// count for whichever tier(s) are flagged true.</summary>
public record SkccMemberRow(string SkccNr, string Callsign, string? Name, DateTime QsoDateUtc,
    bool CountsForCenturion, bool CountsForTribune, bool CountsForSenator);

/// <summary>A progressive SKCC award tier (Centurion/Tribune/Senator) -- CurrentCount toward Required,
/// same "N of M, remaining" idea as ParksOnTheAirViewModel.ComputeAwardTier but expressed per-tier
/// instead of collapsed into one tier name, since SKCC's three tiers are earned independently in
/// sequence (Tribune needs Centurion first; Senator needs Tribune x8 first) rather than being thresholds
/// of the same count.</summary>
public record SkccTierStatus(string TierName, int Required, int CurrentCount, string? Note)
{
    public bool Achieved => CurrentCount >= Required;
    public int Remaining => Math.Max(0, Required - CurrentCount);
    public string Display => Achieved ? $"{CurrentCount} / {Required} -- Achieved" : $"{CurrentCount} / {Required} ({Remaining} remaining)";
    public bool HasNote => !string.IsNullOrEmpty(Note);
}

/// <summary>Tracks progress toward SKCC's Centurion/Tribune/Senator awards, computed live from the
/// operator's own logged QSOs (Qso.SkccNr, the *contacted* station's SKCC number) cross-referenced
/// against the three official award lists (SkccCenturionListDatabase/SkccTribuneListDatabase --
/// SkccSenatorListDatabase is downloaded too but not currently needed for the eligibility math below).
///
/// This is a best-effort estimate, not the official submission tool -- two simplifications versus SKCC's
/// actual rules, called out because they can't be verified from the data CvarcLogger has:
/// 1. SKCC requires every QSO be made with a manual key (straight key/bug/cootie); the app has no field
///    for that, so any CW-mode QSO with an SkccNr filled in is trusted to have been made that way.
/// 2. SKCC also requires the QSO date to be on/after both operators' own join dates; this only checks
///    the *other* station's award-level dates (Centurion/Tribune), not join dates for either side.
///
/// Centurion: 100 unique SKCC members worked (any level). Tribune: 50 unique members who had already
/// reached Centurion-or-higher *at the time of that QSO* (not their current level) -- SKCC's own rules
/// use contact-time status, since a member's suffix keeps advancing over the years. Senator: 200 unique
/// members who were already Tribune-or-higher at contact time, counted only from QSOs on/after the date
/// the operator's own running Tribune count first reached 400 (Tx8) -- derived purely from the operator's
/// own qualifying-QSO chronology, not by looking up the operator's own number in the Tribune list (which
/// would require them to already be registered there for real).</summary>
public partial class SkccViewModel : ObservableObject
{
    // SKCC's own tier requirements -- the single source of truth every count/display/eligibility check
    // below derives from, instead of the four separate literal 100/50/200/400s this replaced.
    private const int CenturionRequired = 100;
    private const int TributeRequired = 50;
    private const int SenatorRequired = 200;
    private const int Tx8QualifyingContacts = TributeRequired * 8;

    private readonly IQsoRepository _qsoRepository;
    private readonly SkccCenturionListDatabase _centurionDb;
    private readonly SkccTribuneListDatabase _tribuneDb;
    private readonly SkccSenatorListDatabase _senatorDb;
    private readonly SkccRefDatabase _rosterDb;
    private readonly DialogService _dialogService;

    private static readonly Regex LeadingDigitsRegex = new(@"^\d+", RegexOptions.Compiled);

    public ObservableCollection<SkccMemberRow> Members { get; } = new();
    public ObservableCollection<SkccTierStatus> Tiers { get; } = new();

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isUpdatingAwardLists;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    public SkccViewModel(
        IQsoRepository qsoRepository,
        SkccCenturionListDatabase centurionDb,
        SkccTribuneListDatabase tribuneDb,
        SkccSenatorListDatabase senatorDb,
        SkccRefDatabase rosterDb,
        DialogService dialogService)
    {
        _qsoRepository = qsoRepository;
        _centurionDb = centurionDb;
        _tribuneDb = tribuneDb;
        _senatorDb = senatorDb;
        _rosterDb = rosterDb;
        _dialogService = dialogService;
    }

    /// <summary>Strips a member's earned-award suffix (e.g. "1234C" -> "1234") so the same person is
    /// recognized across QSOs logged years apart under different suffixes. Returns null if there's no
    /// leading number at all (a malformed entry).</summary>
    private static string? ExtractBaseNumber(string skccNr)
    {
        var match = LeadingDigitsRegex.Match(skccNr.Trim());
        return match.Success ? match.Value : null;
    }

    private static bool TryParseSkccDate(string text, out DateTime date) =>
        DateTime.TryParseExact(text.Trim(), "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Batch-looks-up every given SKCC number in one award list and returns only the ones that
    /// resolved to a parseable award date, keyed by number. One connection total (via
    /// ReferenceDatabase.LookupManyAsync) regardless of how many numbers are passed in.</summary>
    private static async Task<Dictionary<string, DateTime>> ResolveAwardDatesAsync(ReferenceDatabase awardListDb, IEnumerable<string> skccNumbers)
    {
        var infos = await awardListDb.LookupManyAsync(skccNumbers);
        var dates = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nr, info) in infos)
            if (TryParseSkccDate(info.Detail, out var date)) dates[nr] = date;
        return dates;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var qsos = await _qsoRepository.GetAllAsync();
            var candidates = qsos
                .Where(q => !string.IsNullOrWhiteSpace(q.SkccNr) && string.Equals(q.Mode, "CW", StringComparison.OrdinalIgnoreCase))
                .Select(q => (Qso: q, BaseNr: ExtractBaseNumber(q.SkccNr!)))
                .Where(x => x.BaseNr is not null)
                .OrderBy(x => x.Qso.QsoDateTimeOnUtc)
                .ToList();

            var uniqueNumbers = candidates.Select(x => x.BaseNr!).Distinct().ToList();
            var centurionDates = await ResolveAwardDatesAsync(_centurionDb, uniqueNumbers);
            var tribuneDates = await ResolveAwardDatesAsync(_tribuneDb, uniqueNumbers);

            // Phase 1: pure in-memory pass (no I/O) deciding which QSOs count toward which tier. Roster
            // names are deliberately not resolved here -- see phase 2 -- so this stays a single cheap
            // pass over the candidate list instead of one database round trip per counted contact.
            var centurionSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tribuneSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tribuneQualifyingDatesInOrder = new List<DateTime>();
            var counted = new List<(string BaseNr, string Callsign, DateTime QsoDateUtc, bool Centurion, bool Tribune)>();

            foreach (var (qso, baseNr) in candidates)
            {
                bool countsCenturion = centurionSeen.Add(baseNr!);

                bool countsTribune = false;
                if (centurionDates.TryGetValue(baseNr!, out var centurionDate) && qso.QsoDateTimeOnUtc >= centurionDate)
                {
                    countsTribune = tribuneSeen.Add(baseNr!);
                    if (countsTribune) tribuneQualifyingDatesInOrder.Add(qso.QsoDateTimeOnUtc);
                }

                if (countsCenturion || countsTribune)
                    counted.Add((baseNr!, qso.Callsign, qso.QsoDateTimeOnUtc, countsCenturion, countsTribune));
            }

            // Tx8 is derived from the operator's own chronology, not looked up externally -- see the
            // class doc comment for why.
            DateTime? myTx8Date = tribuneQualifyingDatesInOrder.Count >= Tx8QualifyingContacts
                ? tribuneQualifyingDatesInOrder[Tx8QualifyingContacts - 1]
                : null;

            var senatorSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var senatorCounted = new List<(string BaseNr, string Callsign, DateTime QsoDateUtc)>();
            if (myTx8Date is DateTime tx8)
            {
                foreach (var (qso, baseNr) in candidates)
                {
                    if (qso.QsoDateTimeOnUtc < tx8) continue;
                    if (!tribuneDates.TryGetValue(baseNr!, out var tribuneDate) || qso.QsoDateTimeOnUtc < tribuneDate) continue;
                    if (!senatorSeen.Add(baseNr!)) continue;

                    senatorCounted.Add((baseNr!, qso.Callsign, qso.QsoDateTimeOnUtc));
                }
            }

            // Phase 2: one more connection resolves every counted member's roster name at once.
            var neededNumbers = counted.Select(c => c.BaseNr).Concat(senatorCounted.Select(c => c.BaseNr));
            var roster = await _rosterDb.LookupManyAsync(neededNumbers);

            Members.Clear();
            var allRows = counted
                .Select(c => new SkccMemberRow(c.BaseNr, c.Callsign, roster.GetValueOrDefault(c.BaseNr)?.Name, c.QsoDateUtc, c.Centurion, c.Tribune, false))
                .Concat(senatorCounted.Select(c => new SkccMemberRow(c.BaseNr, c.Callsign, roster.GetValueOrDefault(c.BaseNr)?.Name, c.QsoDateUtc, false, false, true)))
                .OrderByDescending(r => r.QsoDateUtc);
            foreach (var row in allRows) Members.Add(row);

            int tribuneTier = tribuneSeen.Count / TributeRequired;
            int senatorTier = senatorSeen.Count / SenatorRequired;

            Tiers.Clear();
            Tiers.Add(new SkccTierStatus("Centurion", CenturionRequired, centurionSeen.Count, null));
            Tiers.Add(new SkccTierStatus("Tribune", TributeRequired, tribuneSeen.Count,
                tribuneTier > 0 ? $"Tx{tribuneTier} reached" : centurionSeen.Count < CenturionRequired ? "Requires Centurion first" : null));
            Tiers.Add(new SkccTierStatus("Senator", SenatorRequired, senatorSeen.Count,
                senatorTier > 0 ? $"Sx{senatorTier} reached" : myTx8Date is null ? $"Requires Tribune x8 ({Tx8QualifyingContacts} qualifying contacts) first" : null));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not compute SKCC progress: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>"Update SKCC Award Lists" button: downloads/rebuilds all three local award-tier lists
    /// (Centurion, Tribune, Senator) so the eligibility cross-check above works offline. Does not reload
    /// Members/Tiers itself -- callers should follow a successful update with LoadAsync.</summary>
    [RelayCommand]
    private async Task UpdateAwardListsAsync()
    {
        IsUpdatingAwardLists = true;
        try
        {
            int centurionCount = await _centurionDb.UpdateAsync();
            int tribuneCount = await _tribuneDb.UpdateAsync();
            int senatorCount = await _senatorDb.UpdateAsync();

            if (centurionCount > 0 && tribuneCount > 0 && senatorCount > 0)
            {
                _dialogService.ShowInfo(
                    $"SKCC award lists updated: {centurionCount:N0} Centurions, {tribuneCount:N0} Tribunes, {senatorCount:N0} Senators.");
                await LoadAsync();
            }
            else
            {
                _dialogService.ShowError("Could not update one or more SKCC award lists. Check your connection and try again.");
            }
        }
        finally
        {
            IsUpdatingAwardLists = false;
        }
    }
}
