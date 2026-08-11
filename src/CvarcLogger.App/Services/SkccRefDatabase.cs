using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC (Straight Key Century Club) member roster database (skcc-ref.db in
/// App.DataDirectory), built from the club's public member list. Used to confirm a station's SKCC
/// number during a QSO: the operator types the number the other station sent, and this looks up their
/// callsign/name/location so a mis-copied digit is obvious immediately. See ReferenceDatabase.LookupAsync
/// and QsoEntryViewModel.ResolveSkccNrAsync.</summary>
public class SkccRefDatabase : SkccPipeDelimitedDatabase
{
    public SkccRefDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-ref.db";

    protected override string SourceUrl => "https://www.skccgroup.com/search/skcclist.txt";

    /// <summary>skccnr|call|name|city|state|ccnr|mbrdate. skccnr carries an award-level suffix once
    /// earned (e.g. "1234C" = Centurion, "1234T" = Tribune, "1234S" = Senator) -- exactly what an
    /// operator reads out over the air, so it's stored and matched verbatim rather than stripped.</summary>
    protected override (string Reference, string Name, string Detail)? MapRow(string[] fields)
    {
        if (fields.Length < 5) return null;
        string skccNr = fields[0], call = fields[1];
        if (skccNr.Length == 0 || call.Length == 0) return null;

        string detail = string.Join(", ", new[] { fields[2], fields[3], fields[4] }.Where(s => s.Length > 0));
        return (skccNr, call, detail);
    }
}
