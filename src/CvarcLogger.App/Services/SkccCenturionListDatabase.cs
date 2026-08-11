using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>The on-device SKCC Centurion award list (skcc-centurion.db in App.DataDirectory) -- every
/// member who has earned Centurion, and the date they earned it. Used by SkccViewModel to determine
/// whether a contacted member had already reached Centurion (or higher) *at the time of a given QSO*,
/// which is what actually counts toward the operator's own Tribune award (SKCC's own submission rules
/// use the contact's status at contact time, not their current status). Column layout parsed by the
/// shared SkccAwardListDatabase base.</summary>
public class SkccCenturionListDatabase : SkccAwardListDatabase
{
    public SkccCenturionListDatabase(HttpClient httpClient) : base(httpClient) { }

    protected override string FileName => "skcc-centurion.db";

    protected override string SourceUrl => "https://www.skccgroup.com/centurionlist.txt";
}
