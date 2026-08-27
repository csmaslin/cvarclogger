using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SOTA summit reference database (sota-ref.db in App.DataDirectory), built
/// from the official worldwide summit list -- richer than the flat-CSV cache SotaSummitLookupService
/// uses (that one only keeps code/name/points; this keeps altitude too, and is a proper indexed
/// database rather than a linear file scan per lookup). Used to show summit info when the operator
/// types a SOTA reference on the entry form; see ReferenceDatabase.LookupAsync.</summary>
public class SotaRefDatabase : ReferenceDatabase
{
    public SotaRefDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "sota-ref.db";

    protected override string SourceUrl => "https://storage.sota.org.uk/summitslist.csv";

    /// <summary>CSV columns: 0=SummitCode 3=SummitName 4=AltM 10=Points. Line 0 is a title, line 1 the
    /// header.</summary>
    protected override IEnumerable<(string Reference, string Name, string Detail)> Parse(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        reader.ReadLine(); // title: "SOTA Summits List (Date=...)"
        reader.ReadLine(); // column header

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var f = ParseCsvLine(line);
            if (f.Length < 11) continue;
            if (f[0].Length == 0 || f[3].Length == 0) continue;

            string detail = int.TryParse(f[4], out int altM) && int.TryParse(f[10], out int points)
                ? $"{altM} m, {points} pts"
                : string.Empty;

            yield return (f[0], f[3], detail);
        }
    }
}
