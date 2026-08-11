using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC Tribune award list (skcc-tribune.db in App.DataDirectory) -- every
/// member who has earned Tribune, and the date they first earned it (Tx1). Used the same way as
/// SkccCenturionListDatabase, but for determining Senator-award eligibility: a contact only counts
/// toward Senator if the other member had already reached Tribune (or higher) at contact time. Column
/// layout parsed by the shared SkccAwardListDatabase base.</summary>
public class SkccTribuneListDatabase : SkccAwardListDatabase
{
    public SkccTribuneListDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-tribune.db";

    protected override string SourceUrl => "https://www.skccgroup.com/tribunelist.txt";
}
