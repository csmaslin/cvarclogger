using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC Tribune award list (skcc-tribune.db in App.DataDirectory) -- every
/// member who has earned Tribune, and the date they first earned it (Tx1). Used the same way as
/// SkccCenturionListDatabase, but for determining Senator-award eligibility: a contact only counts
/// toward Senator if the other member had already reached Tribune (or higher) at contact time.</summary>
public class SkccTribuneListDatabase : ReferenceDatabase
{
    public SkccTribuneListDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-tribune.db";

    protected override string SourceUrl => "https://www.skccgroup.com/tribunelist.txt";

    /// <summary>Pipe-delimited: tnr|call|skccnr|name|city|state|tdate|tendorsements. Keyed by skccnr
    /// (column 2), same rationale as SkccCenturionListDatabase.Parse.</summary>
    protected override IEnumerable<(string Reference, string Name, string Detail)> Parse(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        reader.ReadLine(); // header: tnr|call|skccnr|name|city|state|tdate|tendorsements

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var f = line.Split('|');
            if (f.Length < 7) continue;
            string skccNr = f[2], call = f[1], tdate = f[6];
            if (skccNr.Length == 0 || call.Length == 0 || tdate.Length == 0) continue;

            yield return (skccNr, call, tdate);
        }
    }
}
