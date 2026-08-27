using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device POTA park reference database (pota-ref.db in App.DataDirectory), built from
/// POTA's full park export -- richer than the bundled PotaParks.csv snapshot PotaParkLookupService falls
/// back to (that one only keeps reference/name; this keeps the location description and grid too, and is
/// a proper indexed database rather than an in-memory dictionary rebuilt from a static file). Used to
/// show park info when the operator types a POTA reference on the entry form; see
/// ReferenceDatabase.LookupAsync.</summary>
public class PotaRefDatabase : ReferenceDatabase
{
    public PotaRefDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "pota-ref.db";

    protected override string SourceUrl => "https://pota.app/all_parks_ext.csv";

    /// <summary>CSV columns: reference, name, active, entityId, locationDesc, latitude, longitude, grid.
    /// Only active parks are imported.</summary>
    protected override IEnumerable<(string Reference, string Name, string Detail)> Parse(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        reader.ReadLine(); // header

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var f = ParseCsvLine(line);
            if (f.Length < 8) continue;
            if (f[0].Length == 0 || f[1].Length == 0) continue;
            if (f[2] != "1") continue; // inactive park

            string detail = f[7].Length > 0 ? $"{f[4]}, {f[7]}" : f[4];

            yield return (f[0], f[1], detail);
        }
    }
}
