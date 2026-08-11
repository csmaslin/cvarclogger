using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC Centurion award list (skcc-centurion.db in App.DataDirectory) -- every
/// member who has earned Centurion, and the date they earned it. Used by SkccViewModel to determine
/// whether a contacted member had already reached Centurion (or higher) *at the time of a given QSO*,
/// which is what actually counts toward the operator's own Tribune award (SKCC's own submission rules
/// use the contact's status at contact time, not their current status).</summary>
public class SkccCenturionListDatabase : ReferenceDatabase
{
    public SkccCenturionListDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-centurion.db";

    protected override string SourceUrl => "https://www.skccgroup.com/centurionlist.txt";

    /// <summary>Pipe-delimited: cnr|call|skccnr|name|city|state|cdate|cendorsements. Keyed by skccnr
    /// (column 2) rather than cnr (column 0, just the Centurion award's own sequence number) so a
    /// contacted member can be looked up by the SKCC number they gave over the air.</summary>
    protected override IEnumerable<(string Reference, string Name, string Detail)> Parse(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        reader.ReadLine(); // header: cnr|call|skccnr|name|city|state|cdate|cendorsements

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var f = line.Split('|');
            if (f.Length < 7) continue;
            string skccNr = f[2], call = f[1], cdate = f[6];
            if (skccNr.Length == 0 || call.Length == 0 || cdate.Length == 0) continue;

            yield return (skccNr, call, cdate);
        }
    }
}
