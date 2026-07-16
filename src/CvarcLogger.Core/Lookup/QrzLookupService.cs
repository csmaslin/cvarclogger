using System.Globalization;
using System.Xml.Linq;
using CvarcLogger.Core.Abstractions;

namespace CvarcLogger.Core.Lookup;

/// <summary>QRZ.com XML Data API (requires a paid XML subscription). See
/// SessionKeyXmlLookupServiceBase for the shared login/session/retry protocol — this class only
/// supplies QRZ's URLs and its &lt;Callsign&gt; element's field names.</summary>
public class QrzLookupService : SessionKeyXmlLookupServiceBase
{
    public const string CredentialKey = "QRZ";

    public QrzLookupService(HttpClient httpClient, ICredentialStore credentialStore)
        : base(httpClient, credentialStore, CredentialKey, "QRZ.com")
    {
    }

    protected override string BuildLoginUrl(string username, string password) =>
        $"https://xmldata.qrz.com/xml/current/?username={Uri.EscapeDataString(username)}" +
        $";password={Uri.EscapeDataString(password)};agent=CvarcLogger";

    protected override string BuildLookupUrl(string sessionKey, string callsign) =>
        $"https://xmldata.qrz.com/xml/current/?s={Uri.EscapeDataString(sessionKey)}" +
        $";callsign={Uri.EscapeDataString(callsign)}";

    protected override CallsignLookupResult ParseCallsign(XElement callEl, XNamespace ns)
    {
        string? name = JoinNonEmpty(" ", callEl.Element(ns + "fname")?.Value, callEl.Element(ns + "name")?.Value);
        string? grid = callEl.Element(ns + "grid")?.Value;
        string? country = callEl.Element(ns + "country")?.Value;
        string? state = callEl.Element(ns + "state")?.Value;
        string? county = callEl.Element(ns + "county")?.Value;
        string? city = callEl.Element(ns + "addr2")?.Value;
        int? dxcc = int.TryParse(callEl.Element(ns + "dxcc")?.Value, out var d) ? d : null;
        double? lat = double.TryParse(callEl.Element(ns + "lat")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var la) ? la : null;
        double? lon = double.TryParse(callEl.Element(ns + "lon")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) ? lo : null;

        return new CallsignLookupResult(
            Found: true, Name: name, GridSquare: grid, Country: country, DxccEntityCode: dxcc,
            State: state, County: county, City: city, Latitude: lat, Longitude: lon);
    }

    private static string? JoinNonEmpty(string separator, params string?[] parts)
    {
        string joined = string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrEmpty(joined) ? null : joined;
    }
}
