using System.Xml;
using System.Xml.Linq;
using CvarcLogger.Core.Abstractions;

namespace CvarcLogger.Core.Lookup;

/// <summary>Base for callsign lookup services built on the QRZ.com-style XML session-key protocol:
/// log in once with username/password to get a session key, cache it in memory for the lifetime of
/// the instance, use it for lookups, and re-authenticate once if a lookup reports the session has
/// expired. QRZ.com and QRZCQ.com both use this exact protocol shape (root element with a
/// &lt;Session&gt; and a &lt;Callsign&gt; element) — only the URLs and the Callsign element's field
/// names differ, which is all a subclass needs to supply.</summary>
public abstract class SessionKeyXmlLookupServiceBase : ICallsignLookupService
{
    private readonly HttpClient _httpClient;
    private readonly ICredentialStore _credentialStore;
    private readonly string _credentialKey;
    private string? _sessionKey;

    public string ServiceName { get; }

    protected SessionKeyXmlLookupServiceBase(
        HttpClient httpClient, ICredentialStore credentialStore, string credentialKey, string serviceName)
    {
        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _credentialKey = credentialKey;
        ServiceName = serviceName;
    }

    public async Task<CallsignLookupResult> LookupAsync(string callsign, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(callsign))
            return CallsignLookupResult.NotFound("Callsign is empty.");

        var credentials = await _credentialStore.LoadAsync(_credentialKey, ct).ConfigureAwait(false);
        if (credentials is null)
            return CallsignLookupResult.NotFound($"No {ServiceName} credentials configured.");

        return await LookupWithSessionAsync(callsign, credentials.Value, retryOnAuthFailure: true, ct)
            .ConfigureAwait(false);
    }

    private async Task<CallsignLookupResult> LookupWithSessionAsync(
        string callsign, (string Username, string Password) credentials, bool retryOnAuthFailure, CancellationToken ct)
    {
        try
        {
            if (_sessionKey is null)
            {
                var loginError = await LoginAsync(credentials.Username, credentials.Password, ct).ConfigureAwait(false);
                if (loginError is not null)
                    return loginError;
            }

            string url = BuildLookupUrl(_sessionKey!, callsign.Trim());
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = XDocument.Parse(xml);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            string? sessionError = doc.Root?.Element(ns + "Session")?.Element(ns + "Error")?.Value;
            if (!string.IsNullOrEmpty(sessionError))
            {
                if (retryOnAuthFailure)
                {
                    _sessionKey = null;
                    return await LookupWithSessionAsync(callsign, credentials, retryOnAuthFailure: false, ct)
                        .ConfigureAwait(false);
                }
                return CallsignLookupResult.NotFound(sessionError);
            }

            var callEl = doc.Root?.Element(ns + "Callsign");
            if (callEl is null)
                return CallsignLookupResult.NotFound($"Callsign not found on {ServiceName}.");

            return ParseCallsign(callEl, ns);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException)
        {
            return CallsignLookupResult.NotFound(ex.Message);
        }
    }

    /// <summary>Returns null on success (session key cached), or a NotFound result describing the failure.</summary>
    private async Task<CallsignLookupResult?> LoginAsync(string username, string password, CancellationToken ct)
    {
        string url = BuildLoginUrl(username, password);
        using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var sessionEl = doc.Root?.Element(ns + "Session");
        string? key = sessionEl?.Element(ns + "Key")?.Value;
        string? error = sessionEl?.Element(ns + "Error")?.Value;

        if (!string.IsNullOrEmpty(error))
            return CallsignLookupResult.NotFound(error);

        if (string.IsNullOrEmpty(key))
            return CallsignLookupResult.NotFound($"{ServiceName} login failed: no session key returned.");

        _sessionKey = key;
        return null;
    }

    protected abstract string BuildLoginUrl(string username, string password);
    protected abstract string BuildLookupUrl(string sessionKey, string callsign);
    protected abstract CallsignLookupResult ParseCallsign(XElement callsignElement, XNamespace ns);
}
