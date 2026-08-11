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
    public string Display => $"{CurrentCount} / {Required}" + (Achieved ? " -- Achieved" : "");
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
            var centurionDates = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            var tribuneDates = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var nr in uniqueNumbers)
            {
                var c = await _centurionDb.LookupAsync(nr);
                if (c is not null && TryParseSkccDate(c.Detail, out var cd)) centurionDates[nr] = cd;

                var t = await _tribuneDb.LookupAsync(nr);
                if (t is not null && TryParseSkccDate(t.Detail, out var td)) tribuneDates[nr] = td;
            }

            var rows = new List<SkccMemberRow>();

            // Centurion: every unique member worked counts, first QSO with them is the counted one.
            var centurionSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Tribune: only QSOs where the contact had already reached Centurion+ at contact time.
            var tribuneSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tribuneQualifyingDatesInOrder = new List<DateTime>();

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
                {
                    var roster = await _rosterDb.LookupAsync(baseNr!);
                    rows.Add(new SkccMemberRow(baseNr!, qso.Callsign, roster?.Name, qso.QsoDateTimeOnUtc,
                        countsCenturion, countsTribune, false));
                }
            }

            // Tx8 (400 qualifying Tribune contacts) is derived from the operator's own chronology, not
            // looked up externally -- see the class doc comment for why.
            DateTime? myTx8Date = tribuneQualifyingDatesInOrder.Count >= 400 ? tribuneQualifyingDatesInOrder[399] : null;

            var senatorSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var senatorRows = new List<SkccMemberRow>();
            if (myTx8Date is DateTime tx8)
            {
                foreach (var (qso, baseNr) in candidates)
                {
                    if (qso.QsoDateTimeOnUtc < tx8) continue;
                    if (!tribuneDates.TryGetValue(baseNr!, out var tribuneDate) || qso.QsoDateTimeOnUtc < tribuneDate) continue;
                    if (!senatorSeen.Add(baseNr!)) continue;

                    var roster = await _rosterDb.LookupAsync(baseNr!);
                    senatorRows.Add(new SkccMemberRow(baseNr!, qso.Callsign, roster?.Name, qso.QsoDateTimeOnUtc,
                        false, false, true));
                }
            }

            Members.Clear();
            foreach (var row in rows.Concat(senatorRows).OrderByDescending(r => r.QsoDateUtc)) Members.Add(row);

            int tribuneTier = tribuneSeen.Count / 50;
            int senatorTier = senatorSeen.Count / 200;

            Tiers.Clear();
            Tiers.Add(new SkccTierStatus("Centurion", 100, centurionSeen.Count, null));
            Tiers.Add(new SkccTierStatus("Tribune", 50, tribuneSeen.Count,
                tribuneTier > 0 ? $"Tx{tribuneTier} reached" : centurionSeen.Count < 100 ? "Requires Centurion first" : null));
            Tiers.Add(new SkccTierStatus("Senator", 200, senatorSeen.Count,
                senatorTier > 0 ? $"Sx{senatorTier} reached" : myTx8Date is null ? "Requires Tribune x8 (400 qualifying contacts) first" : null));
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
