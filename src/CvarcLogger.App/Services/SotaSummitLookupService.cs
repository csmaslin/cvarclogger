using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

public record SotaSummitInfo(
    string SummitCode,
    string SummitName,
    int Points,
    string Region = "",
    string Elevation = "",
    string Latitude = "",
    string Longitude = "",
    string SeasonalBonus = "",
    int S2SPoints = 0);

/// <summary>Resolves SOTA summit points/name by summit code from the official SOTA summit list
/// (https://storage.sota.org.uk/summitslist.csv, ~181,000 summits worldwide, ~25MB). Too large to
/// embed as a compiled resource like the DXCC seed data is, so it's cached to disk instead and
/// refreshed only when missing or stale.</summary>
public class SotaSummitLookupService
{
    private const string SummitsListUrl = "https://storage.sota.org.uk/summitslist.csv";
    private static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(30);

    private readonly HttpClient _httpClient;

    public SotaSummitLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private static string CachePath => Path.Combine(App.AppDirectory, "sota-summitslist.csv");

    public async Task<SotaSummitInfo?> LookupAsync(string summitCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(summitCode)) return null;

        await EnsureCacheFreshAsync(force: false, ct).ConfigureAwait(false);

        string needle = Normalize(summitCode);
        // Cheap pre-check before the full quoted-CSV split: the association prefix (e.g. "W6/") is
        // unaffected by hyphen/whitespace normalization, so it's still a valid fast filter even
        // though the full comparison below is normalized.
        int slash = summitCode.IndexOf('/');
        string? prefixCheck = slash > 0 ? summitCode[..(slash + 1)].Trim() : null;

        using var reader = new StreamReader(CachePath);
        await reader.ReadLineAsync().ConfigureAwait(false); // title line: "SOTA Summits List (Date=...)"
        await reader.ReadLineAsync().ConfigureAwait(false); // column header line

        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (line.Length == 0) continue;
            if (prefixCheck is not null && !line.StartsWith(prefixCheck, StringComparison.OrdinalIgnoreCase)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Length <= 10) continue;

            if (Normalize(fields[0]) == needle && int.TryParse(fields[10], out int points))
            {
                string region = fields.Length > 2 ? fields[2] : "";
                string elevation = fields.Length > 4 ? fields[4] : "";
                string latitude = fields.Length > 7 ? fields[7] : "";
                string longitude = fields.Length > 6 ? fields[6] : "";
                string seasonalBonus = fields.Length > 11 ? fields[11] : "";
                int s2sPoints = 0;
                if (fields.Length > 12 && int.TryParse(fields[12], out int s2s))
                {
                    s2sPoints = s2s;
                }

                return new SotaSummitInfo(fields[0], fields[3], points, region, elevation, latitude, longitude, seasonalBonus, s2sPoints);
            }
        }

        return null;
    }

    /// <summary>Normalizes a SOTA summit code for comparison -- uppercase, hyphens/whitespace
    /// stripped -- so a common typo like a missing hyphen ("W6/CC003" instead of "W6/CC-003")
    /// still resolves correctly. Public so callers (e.g. grouping QSOs by summit) can normalize
    /// the same way before this service ever sees the code.</summary>
    public static string Normalize(string code) =>
        code.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

    /// <summary>Returns the total count of summits in the cached SOTA database, or 0 if the cache
    /// doesn't exist or can't be read.</summary>
    public async Task<int> GetSummitCountAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureCacheFreshAsync(force: false, ct).ConfigureAwait(false);
            if (!File.Exists(CachePath)) return 0;

            int count = 0;
            using var reader = new StreamReader(CachePath);
            await reader.ReadLineAsync().ConfigureAwait(false); // title line
            await reader.ReadLineAsync().ConfigureAwait(false); // column header line

            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (line.Length > 0) count++;
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Forces a fresh download of the summit list regardless of the cache's age, for the
    /// user-triggered "Refresh Summit List" button -- unlike the normal 30-day staleness check, this
    /// always hits the network.</summary>
    public Task RefreshAsync(CancellationToken ct = default) => EnsureCacheFreshAsync(force: true, ct);

    private async Task EnsureCacheFreshAsync(bool force, CancellationToken ct)
    {
        bool stale = force || !File.Exists(CachePath) ||
            (DateTime.UtcNow - File.GetLastWriteTimeUtc(CachePath)) > MaxCacheAge;
        if (!stale) return;

        Directory.CreateDirectory(App.AppDirectory);
        byte[] data = await _httpClient.GetByteArrayAsync(SummitsListUrl, ct).ConfigureAwait(false);
        await File.WriteAllBytesAsync(CachePath, data, ct).ConfigureAwait(false);
    }

    /// <summary>Splits one CSV line respecting double-quoted fields, which may contain embedded
    /// commas (e.g. region names like "Alaska - Anchorage" or multi-callsign activation entries).</summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
