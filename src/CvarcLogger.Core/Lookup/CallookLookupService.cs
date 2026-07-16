using System.Globalization;
using System.Text.Json;

namespace CvarcLogger.Core.Lookup;

/// <summary>Free, no-auth lookup against callook.info (backed by FCC ULS data). US callsigns only.</summary>
public class CallookLookupService : ICallsignLookupService
{
    private readonly HttpClient _httpClient;

    public string ServiceName => "callook.info";

    public CallookLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CallsignLookupResult> LookupAsync(string callsign, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(callsign))
            return CallsignLookupResult.NotFound("Callsign is empty.");

        try
        {
            using var response = await _httpClient
                .GetAsync($"https://callook.info/{Uri.EscapeDataString(callsign.Trim())}/json", ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return CallsignLookupResult.NotFound($"HTTP {(int)response.StatusCode}");

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            string? status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            if (!string.Equals(status, "VALID", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "UPDATED", StringComparison.OrdinalIgnoreCase))
            {
                return CallsignLookupResult.NotFound("Callsign not found in FCC database.");
            }

            string? name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;

            string? grid = null;
            double? lat = null, lon = null;
            if (root.TryGetProperty("location", out var loc))
            {
                grid = loc.TryGetProperty("gridsquare", out var g) ? g.GetString() : null;
                if (loc.TryGetProperty("latitude", out var latEl) &&
                    double.TryParse(latEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var la))
                    lat = la;
                if (loc.TryGetProperty("longitude", out var lonEl) &&
                    double.TryParse(lonEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo))
                    lon = lo;
            }

            string? state = null;
            string? city = null;
            if (root.TryGetProperty("address", out var addr) && addr.TryGetProperty("line2", out var line2El))
            {
                // "line2" is typically "City, ST ZIP"
                var line2 = line2El.GetString();
                if (!string.IsNullOrWhiteSpace(line2))
                {
                    var parts = line2.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1) city = parts[0];
                    if (parts.Length >= 2)
                    {
                        var stateZip = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (stateZip.Length >= 1) state = stateZip[0];
                    }
                }
            }

            return new CallsignLookupResult(
                Found: true,
                Name: name,
                GridSquare: grid,
                Country: "United States",
                DxccEntityCode: 291,
                State: state,
                City: city,
                Latitude: lat,
                Longitude: lon);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return CallsignLookupResult.NotFound(ex.Message);
        }
    }
}
