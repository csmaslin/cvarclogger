using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.ViewModels;

/// <summary>Worked All Countries (ARRL DXCC) progress -- computed live from the QSO log. Port of
/// CvarcCellLog's DxccViewModel (see that file's doc comment for the full rationale): fetches the log and
/// the DXCC entity list exactly once per page load (LoadAsync), then does every subsequent computation
/// (Band picker changes, the Phone/CW/Digital columns, the 5-Band DXCC strip) in memory against that one
/// snapshot, instead of going back to AwardsService per filter combination. Kept as a near-duplicate
/// rather than shared, matching CvarcCellLog's own standing rule about not pushing app-specific view logic
/// into the shared Core library.</summary>
public partial class DxccViewModel : ObservableObject
{
    private static readonly string[] PhoneModes = { "SSB", "FM", "AM" };
    private static readonly string[] CwModes = { "CW" };
    private static readonly string[] DigitalModes = { "FT8", "FT4", "RTTY", "PSK", "DIGITALVOICE" };

    private static readonly string[] FiveBandDxccBands = { "80m", "40m", "20m", "15m", "10m" };

    private readonly IAwardsService _awardsService;
    private readonly IQsoRepository _qsoRepository;
    private readonly IDxccEntityRepository _dxccEntityRepository;

    private List<Qso> _qsos = new();
    private Dictionary<int, string> _entityNames = new();

    public IReadOnlyList<string> Bands { get; } = new[] { "All Bands" }.Concat(QsoFieldOptions.Bands).ToList();
    public ObservableCollection<FiveBandDxccRow> FiveBandProgress { get; } = new();
    public ObservableCollection<BandQsoCount> BandCounts { get; } = new();
    public ObservableCollection<DxccEntityModeRow> EntityRows { get; } = new();

    [ObservableProperty] private string selectedBand = "All Bands";
    [ObservableProperty] private DxccProgress? progress;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    private string? BandFilter => SelectedBand == "All Bands" ? null : SelectedBand;

    public DxccViewModel(IAwardsService awardsService, IQsoRepository qsoRepository, IDxccEntityRepository dxccEntityRepository)
    {
        _awardsService = awardsService;
        _qsoRepository = qsoRepository;
        _dxccEntityRepository = dxccEntityRepository;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            // Single round trip: this also triggers AwardsService's entity-backfill for any QSO still
            // missing a resolved DxccEntityCode. The returned progress itself is unused here.
            await _awardsService.ComputeDxccProgressAsync();

            _qsos = await _qsoRepository.GetAllAsync();
            _entityNames = (await _dxccEntityRepository.GetAllWithPrefixesAsync())
                .ToDictionary(e => e.EntityCode, e => e.EntityName);

            RefreshSelected();
            RefreshFiveBand();

            // Independent of the Band picker above -- always every band's total, not just the currently
            // selected one -- so one extra round trip here is fine, unlike the per-filter-change DXCC
            // computations this page deliberately keeps local to the in-memory snapshot.
            BandCounts.Clear();
            foreach (var count in await _awardsService.ComputeQsoCountsByBandAsync())
                BandCounts.Add(count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not compute DXCC progress: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedBandChanged(string value) => RefreshSelected();

    private void RefreshSelected()
    {
        try
        {
            Progress = ComputeProgress(null, BandFilter);

            var phone = ComputeProgress(PhoneModes, BandFilter);
            var cw = ComputeProgress(CwModes, BandFilter);
            var digital = ComputeProgress(DigitalModes, BandFilter);

            var phoneCodes = phone.Entities.Select(e => e.EntityCode).ToHashSet();
            var cwCodes = cw.Entities.Select(e => e.EntityCode).ToHashSet();
            var digitalCodes = digital.Entities.Select(e => e.EntityCode).ToHashSet();

            var allEntities = phone.Entities.Concat(cw.Entities).Concat(digital.Entities)
                .GroupBy(e => e.EntityCode)
                .Select(g => g.First())
                .OrderBy(e => e.EntityName, StringComparer.OrdinalIgnoreCase);

            EntityRows.Clear();
            foreach (var entity in allEntities)
            {
                EntityRows.Add(new DxccEntityModeRow(
                    entity.EntityName,
                    phoneCodes.Contains(entity.EntityCode),
                    cwCodes.Contains(entity.EntityCode),
                    digitalCodes.Contains(entity.EntityCode)));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not compute DXCC progress: {ex.Message}";
        }
    }

    /// <summary>5-Band DXCC requires 100+ confirmed entities on each of 80/40/20/15/10m individually.</summary>
    private void RefreshFiveBand()
    {
        try
        {
            FiveBandProgress.Clear();
            foreach (var band in FiveBandDxccBands)
            {
                var progress = ComputeProgress(null, band);
                FiveBandProgress.Add(new FiveBandDxccRow(band, progress.ConfirmedCount));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not compute 5-Band DXCC progress: {ex.Message}";
        }
    }

    /// <summary>Mirrors AwardsService.ComputeDxccProgressAsync's own filtering (band: exact match
    /// ordinal-ignore-case; mode: exact match against one of the given mode strings; entity name from
    /// the pre-loaded DxccEntities lookup), but against the already-fetched in-memory QSO snapshot
    /// instead of a fresh database round trip.</summary>
    private DxccProgress ComputeProgress(string[]? modes, string? band)
    {
        IEnumerable<Qso> filtered = _qsos.Where(q => q.DxccEntityCode.HasValue);

        if (!string.IsNullOrWhiteSpace(band))
            filtered = filtered.Where(q => string.Equals(q.Band, band, StringComparison.OrdinalIgnoreCase));

        if (modes is not null)
            filtered = filtered.Where(q => modes.Contains(q.Mode, StringComparer.OrdinalIgnoreCase));

        var statuses = filtered
            .GroupBy(q => q.DxccEntityCode!.Value)
            .Select(group =>
            {
                string entityName = _entityNames.TryGetValue(group.Key, out var name) ? name : $"Entity {group.Key}";
                return new DxccEntityStatus(group.Key, entityName, Worked: true, Confirmed: group.Any(IsConfirmed));
            })
            .OrderBy(s => s.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DxccProgress(statuses.Count, statuses.Count(s => s.Confirmed), statuses);
    }

    private static bool IsConfirmed(Qso q) =>
        q.QslRcvd is QslStatus.Sent or QslStatus.Verified ||
        q.LotwQslRcvd is QslStatus.Sent or QslStatus.Verified;
}

public record FiveBandDxccRow(string Band, int ConfirmedCount)
{
    public bool Qualifies => ConfirmedCount >= 100;
}

/// <summary>One row of the DXCC entity table -- Phone/Cw/Digital are true when at least one QSO with
/// that entity was logged in that mode category (any band currently selected), independent of the
/// overall Worked/Confirmed counts shown in the summary line above.</summary>
public record DxccEntityModeRow(string EntityName, bool Phone, bool Cw, bool Digital);
