using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC Senator award list (skcc-senator.db in App.DataDirectory) -- every
/// member who has earned Senator, and the date it was issued. Not currently consulted for eligibility
/// math (Senator eligibility only needs the Tribune list -- see SkccTribuneListDatabase), but downloaded
/// alongside it for parity/future use. Column layout parsed by the shared SkccAwardListDatabase base.</summary>
public class SkccSenatorListDatabase : SkccAwardListDatabase
{
    public SkccSenatorListDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-senator.db";

    protected override string SourceUrl => "https://www.skccgroup.com/senator.txt";
}
