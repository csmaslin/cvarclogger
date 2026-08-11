using System.Net.Http;

namespace CvarcLogger.App.Services;

/// <summary>Shared column layout for SKCC's Centurion/Tribune/Senator award lists -- all three publish
/// the exact same shape (award-serial|call|skccnr|name|city|state|award-date|endorsements), differing
/// only in which award and which URL. Concrete subclasses supply just FileName/SourceUrl.</summary>
public abstract class SkccAwardListDatabase : SkccPipeDelimitedDatabase
{
    protected SkccAwardListDatabase(HttpClient httpClient) : base(httpClient) { }

    /// <summary>skccnr|call|name|city|state|award-date|endorsements. Keyed by skccnr (column 2) rather
    /// than the award's own sequence number (column 0) so a contacted member can be looked up by the
    /// SKCC number they gave over the air.</summary>
    protected override (string Reference, string Name, string Detail)? MapRow(string[] fields)
    {
        if (fields.Length < 7) return null;
        string skccNr = fields[2], call = fields[1], date = fields[6];
        if (skccNr.Length == 0 || call.Length == 0 || date.Length == 0) return null;

        return (skccNr, call, date);
    }
}
