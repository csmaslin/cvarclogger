using System.Net;
using System.Text;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Lookup;

namespace CvarcLogger.Tests;

/// <summary>Exercises lookup-service response parsing against fixture JSON/XML via a fake
/// HttpMessageHandler — no live network calls.</summary>
public class LookupServiceParsingTests
{
    [Fact]
    public async Task Callook_ParsesValidResponse()
    {
        const string json = """
        {
          "status": "VALID",
          "type": "PERSON",
          "name": "Hiram Percy Maxim",
          "address": { "line1": "225 Main St", "line2": "Newington, CT 06111", "attn": "" },
          "location": { "latitude": "41.7", "longitude": "-72.7", "gridsquare": "FN31pr" }
        }
        """;
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var service = new CallookLookupService(new HttpClient(handler));

        var result = await service.LookupAsync("W1AW");

        Assert.True(result.Found);
        Assert.Equal("Hiram Percy Maxim", result.Name);
        Assert.Equal("FN31pr", result.GridSquare);
        Assert.Equal("CT", result.State);
        Assert.Equal("Newington", result.City);
        Assert.Equal(291, result.DxccEntityCode);
    }

    [Fact]
    public async Task Callook_ReturnsNotFound_ForInvalidCallsign()
    {
        const string json = """{ "status": "INVALID" }""";
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var service = new CallookLookupService(new HttpClient(handler));

        var result = await service.LookupAsync("ZZ9ZZZ");

        Assert.False(result.Found);
    }

    [Fact]
    public async Task Qrz_LogsInThenLooksUpCallsign()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            string query = request.RequestUri!.Query;
            string xml = query.Contains("username=")
                ? "<QRZDatabase><Session><Key>ABC123</Key></Session></QRZDatabase>"
                : "<QRZDatabase><Callsign><call>W1AW</call><fname>Hiram</fname><name>Maxim</name>" +
                  "<grid>FN31pr</grid><country>United States</country><state>CT</state><addr2>Newington</addr2>" +
                  "<dxcc>291</dxcc></Callsign></QRZDatabase>";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "text/xml")
            };
        });

        var service = new QrzLookupService(new HttpClient(handler), new FakeCredentialStore("user", "pass"));

        var result = await service.LookupAsync("W1AW");

        Assert.True(result.Found);
        Assert.Equal("Hiram Maxim", result.Name);
        Assert.Equal("FN31pr", result.GridSquare);
        Assert.Equal("Newington", result.City);
        Assert.Equal(291, result.DxccEntityCode);
    }

    [Fact]
    public async Task Qrz_ReturnsNotFound_WhenNoCredentialsConfigured()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = new QrzLookupService(new HttpClient(handler), new FakeCredentialStore(null, null));

        var result = await service.LookupAsync("W1AW");

        Assert.False(result.Found);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task QrzCq_LogsInThenLooksUpCallsign()
    {
        // Shape confirmed against QRZCQ's own XML API docs: root <QRZCQDatabase> with an xmlns, a
        // single "name" field (not fname/name split like QRZ), "locator" instead of "grid", and no
        // county field at all.
        var handler = new FakeHttpMessageHandler(request =>
        {
            string query = request.RequestUri!.Query;
            string xml = query.Contains("username=")
                ? "<QRZCQDatabase version=\"1.00\" xmlns=\"http://qrzcq.com\"><Session><Key>ABC123</Key></Session></QRZCQDatabase>"
                : "<QRZCQDatabase version=\"1.00\" xmlns=\"http://qrzcq.com\"><Callsign><call>W1AW</call><name>Hiram Maxim</name>" +
                  "<locator>FN31pr</locator><country>United States</country><state>CT</state><city>Newington</city>" +
                  "<dxcc>291</dxcc><latitude>41.7</latitude><longitude>-72.7</longitude></Callsign>" +
                  "<Session><Key>ABC123</Key></Session></QRZCQDatabase>";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "text/xml")
            };
        });

        var service = new QrzCqLookupService(new HttpClient(handler), new FakeCredentialStore("user", "pass"));

        var result = await service.LookupAsync("W1AW");

        Assert.True(result.Found);
        Assert.Equal("Hiram Maxim", result.Name);
        Assert.Equal("FN31pr", result.GridSquare);
        Assert.Equal("Newington", result.City);
        Assert.Equal(291, result.DxccEntityCode);
        Assert.Equal(41.7, result.Latitude);
        Assert.Equal(-72.7, result.Longitude);
        Assert.Null(result.County);
    }

    [Fact]
    public async Task QrzCq_ReturnsNotFound_WhenNoCredentialsConfigured()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = new QrzCqLookupService(new HttpClient(handler), new FakeCredentialStore(null, null));

        var result = await service.LookupAsync("W1AW");

        Assert.False(result.Found);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task QrzCq_SessionError_ReturnsNotFound()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<QRZCQDatabase version=\"1.00\" xmlns=\"http://qrzcq.com\"><Session><Error>Username / password required</Error></Session></QRZCQDatabase>",
                Encoding.UTF8, "text/xml")
        });
        var service = new QrzCqLookupService(new HttpClient(handler), new FakeCredentialStore("user", "wrongpass"));

        var result = await service.LookupAsync("W1AW");

        Assert.False(result.Found);
        Assert.NotNull(result.Error);
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    private class FakeCredentialStore : ICredentialStore
    {
        private readonly (string Username, string Password)? _credential;

        public FakeCredentialStore(string? username, string? password) =>
            _credential = username is null ? null : (username, password!);

        public Task SaveAsync(string key, string username, string password, CancellationToken ct = default) => Task.CompletedTask;

        public Task<(string Username, string Password)?> LoadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_credential);

        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }
}
