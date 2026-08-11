using System.IO;
using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC (Straight Key Century Club) member roster database (skcc-ref.db in
/// App.DataDirectory), built from the club's public member list. Used to confirm a station's SKCC
/// number during a QSO: the operator types the number the other station sent, and this looks up their
/// callsign/name/location so a mis-copied digit is obvious immediately. See ReferenceDatabase.LookupAsync
/// and QsoEntryViewModel.ResolveSkccNrAsync.</summary>
public class SkccRefDatabase : ReferenceDatabase
{
    public SkccRefDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-ref.db";

    protected override string SourceUrl => "https://www.skccgroup.com/search/skcclist.txt";

    /// <summary>Pipe-delimited: skccnr|call|name|city|state|ccnr|mbrdate. skccnr carries an award-level
    /// suffix once earned (e.g. "1234C" = Centurion, "1234T" = Tribune, "1234S" = Senator) -- exactly what
    /// an operator reads out over the air, so it's stored and matched verbatim rather than stripped.</summary>
    protected override IEnumerable<(string Reference, string Name, string Detail)> Parse(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        reader.ReadLine(); // header: skccnr|call|name|city|state|ccnr|mbrdate

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var f = line.Split('|');
            if (f.Length < 5) continue;
            if (f[0].Length == 0 || f[1].Length == 0) continue;

            string detail = string.Join(", ", new[] { f[2], f[3], f[4] }.Where(s => s.Length > 0));
            yield return (f[0], f[1], detail);
        }
    }
}
