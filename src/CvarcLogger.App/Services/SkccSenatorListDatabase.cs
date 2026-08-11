using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC Senator award list (skcc-senator.db in App.DataDirectory) -- every
/// member who has earned Senator, and the date it was issued. Not currently consulted for eligibility
/// math (Senator eligibility only needs the Tribune list -- see SkccTribuneListDatabase), but downloaded
/// alongside it so SkccViewModel can show a contacted member's Senator status too.</summary>
public class SkccSenatorListDatabase : ReferenceDatabase
{
    public SkccSenatorListDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-senator.db";

    protected override string SourceUrl => "https://www.skccgroup.com/senator.txt";

    /// <summary>Pipe-delimited: senatornr|call|skccnr|name|city|state|issued|senatorendorsements. Keyed
    /// by skccnr (column 2), same rationale as SkccCenturionListDatabase.Parse.</summary>
    protected override IEnumerable<(string Reference, string Name, string Detail)> Parse(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        reader.ReadLine(); // header: senatornr|call|skccnr|name|city|state|issued|senatorendorsements

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var f = line.Split('|');
            if (f.Length < 7) continue;
            string skccNr = f[2], call = f[1], issued = f[6];
            if (skccNr.Length == 0 || call.Length == 0 || issued.Length == 0) continue;

            yield return (skccNr, call, issued);
        }
    }
}
