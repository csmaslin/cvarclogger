using System.Globalization;
using System.Xml.Linq;
using CvarcLogger.Core.Abstractions;

namespace CvarcLogger.Core.Lookup;

/// <summary>QRZCQ.com XML API (requires a Premium QRZCQ account). See SessionKeyXmlLookupServiceBase
/// for the shared login/session/retry protocol — this class only supplies QRZCQ's URLs and its
/// &lt;Callsign&gt; element's field names, which differ slightly from QRZ.com's: a single "name" field
/// instead of separate fname/name, "locator" instead of "grid", and no county field at all —
/// LookupCoordinator's cross-service merge is what fills County in from another configured service.</summary>
public class QrzCqLookupService : SessionKeyXmlLookupServiceBase
{
    public const string CredentialKey = "QRZCQ";

    public QrzCqLookupService(HttpClient httpClient, ICredentialStore credentialStore)
        : base(httpClient, credentialStore, CredentialKey, "QRZCQ.com")
    {
    }

    protected override string BuildLoginUrl(string username, string password) =>
        $"https://ssl.qrzcq.com/xml?username={Uri.EscapeDataString(username)}" +
        $";password={Uri.EscapeDataString(password)};agent=CvarcLogger";

    protected override string BuildLookupUrl(string sessionKey, string callsign) =>
        $"https://ssl.qrzcq.com/xml?s={Uri.EscapeDataString(sessionKey)}" +
        $";callsign={Uri.EscapeDataString(callsign)};agent=CvarcLogger";

    protected override CallsignLookupResult ParseCallsign(XElement callEl, XNamespace ns)
    {
        string? name = callEl.Element(ns + "name")?.Value;
        string? grid = callEl.Element(ns + "locator")?.Value;
        string? country = callEl.Element(ns + "country")?.Value;
        string? state = callEl.Element(ns + "state")?.Value;
        string? city = callEl.Element(ns + "city")?.Value;
        int? dxcc = int.TryParse(callEl.Element(ns + "dxcc")?.Value, out var d) ? d : null;
        double? lat = double.TryParse(callEl.Element(ns + "latitude")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var la) ? la : null;
        double? lon = double.TryParse(callEl.Element(ns + "longitude")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) ? lo : null;

        return new CallsignLookupResult(
            Found: true, Name: name, GridSquare: grid, Country: country, DxccEntityCode: dxcc,
            State: state, County: null, City: city, Latitude: lat, Longitude: lon);
    }
}
