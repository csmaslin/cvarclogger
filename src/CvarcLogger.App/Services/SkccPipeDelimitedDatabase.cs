using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>Shared line-reading mechanics for every SKCC pipe-delimited source file (the member roster,
/// and the Centurion/Tribune/Senator award lists): skip the header row, split each remaining line on
/// '|', and let the concrete subclass decide which columns matter. Replaces what were four near-
/// identical Parse loops (one per file) with one.</summary>
public abstract class SkccPipeDelimitedDatabase : ReferenceDatabase
{
    protected SkccPipeDelimitedDatabase(HttpClient httpClient) : base(httpClient) { }

    /// <summary>Maps one already pipe-split data row to (Reference, Name, Detail), or null to skip it
    /// (malformed/too short/missing a required column).</summary>
    protected abstract (string Reference, string Name, string Detail)? MapRow(string[] fields);

    protected override IEnumerable<(string Reference, string Name, string Detail)> Parse(string filePath)
    {
        using var reader = new StreamReader(filePath);
        reader.ReadLine(); // header row

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var row = MapRow(line.Split('|'));
            if (row is not null) yield return row.Value;
        }
    }
}
