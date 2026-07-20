using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace CvarcLogger.App.Services;

public record PotaParkInfo(string Reference, string Name);

/// <summary>Resolves a POTA park's display name by reference. Tries the live POTA API
/// (https://api.pota.app/park/{reference}) first, since POTA has no bulk "all parks" export to keep a
/// local snapshot current with -- falls back to the bundled park list (Assets\PotaParks.csv, ~93,500
/// parks worldwide, a one-time local snapshot) when offline or the API is unreachable, since that's
/// still good enough for a park that hasn't changed since the snapshot was taken.</summary>
public class PotaParkLookupService
{
    private static string CsvPath => Path.Combine(AppContext.BaseDirectory, "Assets", "PotaParks.csv");

    private readonly HttpClient _httpClient;

    // Registered via AddHttpClient, which resolves a new PotaParkLookupService instance each time (a
    // fresh instance every time Awards Progress is opened) -- static so the 93,500-row CSV is still
    // only ever parsed once per app run, not once per window open.
    private static readonly SemaphoreSlim LoadLock = new(1, 1);
    private static Dictionary<string, PotaParkInfo>? _byNormalizedReference;

    public PotaParkLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PotaParkInfo?> LookupAsync(string parkReference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parkReference)) return null;

        var live = await TryLiveLookupAsync(parkReference.Trim(), ct).ConfigureAwait(false);
        if (live is not null) return live;

        var index = await EnsureLoadedAsync(ct).ConfigureAwait(false);
        return index.TryGetValue(Normalize(parkReference), out var info) ? info : null;
    }

    /// <summary>Queries POTA's live single-park endpoint. Best-effort -- any failure (offline, POTA's
    /// API down, an unrecognized reference, a malformed response) just falls back to the bundled
    /// snapshot rather than surfacing an error, since that fallback is a perfectly good answer for any
    /// park that existed at the time the snapshot was taken.</summary>
    private async Task<PotaParkInfo?> TryLiveLookupAsync(string parkReference, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"https://api.pota.app/park/{Uri.EscapeDataString(parkReference)}", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            // An unrecognized reference comes back as a 200 with a JSON `null` body, not a 404 --
            // TryGetProperty throws on anything that isn't a JSON object, so this has to be checked first.
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("reference", out var refProp) || !root.TryGetProperty("name", out var nameProp))
                return null;

            string? reference = refProp.GetString();
            string? name = nameProp.GetString();
            return string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(name)
                ? null
                : new PotaParkInfo(reference, name);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Normalizes a POTA park reference for comparison -- uppercase, hyphens/whitespace
    /// stripped -- so a common typo like a missing hyphen ("US0001" instead of "US-0001") still
    /// resolves correctly. Public so callers (e.g. grouping QSOs by park) can normalize the same way
    /// before this service ever sees the code.</summary>
    public static string Normalize(string reference) =>
        reference.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

    private static async Task<Dictionary<string, PotaParkInfo>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_byNormalizedReference is not null) return _byNormalizedReference;

        await LoadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_byNormalizedReference is not null) return _byNormalizedReference;

            var index = new Dictionary<string, PotaParkInfo>();
            using var reader = new StreamReader(CsvPath);
            await reader.ReadLineAsync().ConfigureAwait(false); // header: "reference","name","active","entityId","locationDesc"

            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (line.Length == 0) continue;

                var fields = ParseCsvLine(line);
                if (fields.Length < 2 || fields[0].Length == 0) continue;

                index[Normalize(fields[0])] = new PotaParkInfo(fields[0], fields[1]);
            }

            _byNormalizedReference = index;
            return index;
        }
        finally
        {
            LoadLock.Release();
        }
    }

    /// <summary>Splits one CSV line respecting double-quoted fields, which may contain embedded
    /// commas (e.g. park names like "Wrangell-St. Elias National Park, Preserve").</summary>
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
