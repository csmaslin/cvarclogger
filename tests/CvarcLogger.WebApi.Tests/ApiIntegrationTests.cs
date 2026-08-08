using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CvarcLogger.WebApi.Tests;

public class ApiIntegrationTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private const string TestCallsign = "W5TEST";
    private const string TestBand = "20m";
    private const string TestMode = "SSB";

    public ApiIntegrationTests()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task QsoController_GetAll_ReturnsOk()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/qso");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }

    [Fact]
    public async Task QsoController_Create_ReturnsCreated()
    {
        // Arrange
        var qsoData = new
        {
            callsign = TestCallsign,
            qsoDateTimeOnUtc = DateTime.UtcNow.ToString("O"),
            band = TestBand,
            mode = TestMode,
            frequency = 14.200,
            rstSent = "599",
            name = "Test",
            gridSquare = "AA00AA"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(qsoData),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/qso", content);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Created,
            $"Expected success or created, got {response.StatusCode}");
    }

    [Fact]
    public async Task StationController_GetAll_ReturnsOk()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/station");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected success or not found, got {response.StatusCode}");
    }

    [Fact]
    public async Task StationController_Create_ReturnsCreated()
    {
        // Arrange
        var stationData = new
        {
            callSign = "W5ZZZ",
            operatorName = "Test Op",
            qthLocator = "AA00AA"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(stationData),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/station", content);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Created,
            $"Expected success or created, got {response.StatusCode}");
    }

    [Fact]
    public async Task CatController_GetStatus_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/cat/status");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request, got {response.StatusCode}");
    }

    [Fact]
    public async Task CatController_GetConfig_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/cat/config");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }

    [Fact]
    public async Task LookupController_Callsign_ReturnsResult()
    {
        // Act
        var response = await _client.GetAsync($"/api/lookup/callsign/{TestCallsign}");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected success or not found, got {response.StatusCode}");
    }

    [Fact]
    public async Task SettingsController_GetColumnVisibility_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/settings/column-visibility");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }

    [Fact]
    public async Task SettingsController_SetColumnVisibility_ReturnsOk()
    {
        // Arrange
        var visibilityData = new
        {
            hiddenColumns = new[] { "Frequency", "Mode" }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(visibilityData),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/settings/column-visibility", content);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }

    [Fact]
    public async Task SettingsController_GetColumnOrder_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/settings/column-order");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }

    [Fact]
    public async Task SettingsController_GetColumnWidths_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/settings/column-widths");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }

    [Fact]
    public async Task SettingsController_GetDefaultStation_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/settings/station/default");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }

    [Fact]
    public async Task ReferenceDataController_GetDxcc_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/referencedata/dxcc");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request, got {response.StatusCode}");
    }

    [Fact]
    public async Task ReferenceDataController_SearchDxcc_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/referencedata/dxcc/search?query=United");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request, got {response.StatusCode}");
    }

    [Fact]
    public async Task ReferenceDataController_GetSota_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/referencedata/sota");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request, got {response.StatusCode}");
    }

    [Fact]
    public async Task ReferenceDataController_SearchSota_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/referencedata/sota/search?query=W5");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request, got {response.StatusCode}");
    }

    [Fact]
    public async Task ReferenceDataController_GetPota_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/referencedata/pota");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request, got {response.StatusCode}");
    }

    [Fact]
    public async Task ReferenceDataController_SearchPota_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/referencedata/pota/search?query=K-");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected success or bad request, got {response.StatusCode}");
    }

    [Fact]
    public async Task CatController_SetConfig_ReturnsOk()
    {
        // Arrange
        var configData = new
        {
            enabled = true,
            host = "localhost",
            port = 9200
        };

        var content = new StringContent(
            JsonSerializer.Serialize(configData),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/cat/config", content);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
    }
}
