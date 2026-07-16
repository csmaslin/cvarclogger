using System.Reflection;

namespace CvarcLogger.Core.Geo;

/// <summary>Looks up CQ/ITU zone from a grid square's center-point lat/lon against a 1°x1°-band
/// run-length-encoded zone map. Ported from grid.radio's radio-zones.js, whose own header attributes
/// the underlying zone geometry to HB9HIL/hamradio-zones-geojson (MIT License) — see cq_zone_rle.txt
/// and itu_zone_rle.txt for the raw data, extracted verbatim from that file. Parsed once and cached;
/// the whole map is a few hundred KB of ints, cheap to hold in memory for the app's lifetime.</summary>
public class GridZoneResolver : IGridZoneResolver
{
    private static readonly Lazy<Dictionary<int, List<(int Start, int End, int Zone)>>> CqBands =
        new(() => ParseRle(LoadEmbeddedResource("cq_zone_rle.txt")));

    private static readonly Lazy<Dictionary<int, List<(int Start, int End, int Zone)>>> ItuBands =
        new(() => ParseRle(LoadEmbeddedResource("itu_zone_rle.txt")));

    public (int? CqZone, int? ItuZone) Resolve(string? gridSquare)
    {
        var latLon = MaidenheadLocator.ToLatLon(gridSquare);
        if (latLon is null) return (null, null);

        int? cq = LookupZone(latLon.Value.Lat, latLon.Value.Lon, CqBands.Value);
        int? itu = LookupZone(latLon.Value.Lat, latLon.Value.Lon, ItuBands.Value);
        return (cq, itu);
    }

    private static int? LookupZone(double lat, double lon, Dictionary<int, List<(int Start, int End, int Zone)>> bands)
    {
        int latIdx = Math.Clamp((int)Math.Floor(lat + 90), 0, 179);
        int lonIdx = Math.Clamp((int)Math.Floor(lon + 180), 0, 359);

        if (!bands.TryGetValue(latIdx, out var runs)) return null;

        foreach (var (start, end, zone) in runs)
        {
            if (lonIdx >= start && lonIdx < end) return zone;
        }
        return null;
    }

    /// <summary>Mirrors the source's _parseZoneRLE: 180 '|'-separated latitude bands (index = degrees
    /// north of -90), each a comma-separated list of "start.end.zone" longitude runs (index = degrees
    /// east of -180, half-open [start, end)).</summary>
    private static Dictionary<int, List<(int Start, int End, int Zone)>> ParseRle(string encoded)
    {
        var lookup = new Dictionary<int, List<(int, int, int)>>();
        string[] bands = encoded.Split('|');

        for (int latIdx = 0; latIdx < bands.Length; latIdx++)
        {
            string band = bands[latIdx];
            if (string.IsNullOrEmpty(band)) continue;

            var runs = new List<(int, int, int)>();
            foreach (string run in band.Split(','))
            {
                string[] parts = run.Split('.');
                if (parts.Length >= 3 &&
                    int.TryParse(parts[0], out int start) &&
                    int.TryParse(parts[1], out int end) &&
                    int.TryParse(parts[2], out int zone))
                {
                    runs.Add((start, end, zone));
                }
            }
            if (runs.Count > 0) lookup[latIdx] = runs;
        }

        return lookup;
    }

    private static string LoadEmbeddedResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
