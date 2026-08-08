using Microsoft.AspNetCore.Mvc;
using CvarcLogger.App.Services;
using Serilog;

namespace CvarcLogger.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatController : ControllerBase
{
    private readonly InternetCatCoordinator _internetCat;
    private readonly SettingsService _settings;

    public CatController(InternetCatCoordinator internetCat, SettingsService settings)
    {
        _internetCat = internetCat;
        _settings = settings;
    }

    [HttpPost("connect")]
    public async Task<ActionResult> Connect(CancellationToken ct = default)
    {
        try
        {
            var (success, error) = await _internetCat.ConnectAsync(ct);
            if (success)
                return Ok(new { message = "Connected to radio", status = "connected" });
            return BadRequest(new { error = error ?? "Failed to connect to radio" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error connecting to CAT");
            return StatusCode(500, new { error = "Failed to connect to CAT" });
        }
    }

    [HttpPost("disconnect")]
    public async Task<ActionResult> Disconnect()
    {
        try
        {
            await _internetCat.DisconnectAsync();
            return Ok(new { message = "Disconnected from radio", status = "disconnected" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disconnecting from CAT");
            return StatusCode(500, new { error = "Failed to disconnect from CAT" });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetStatus(CancellationToken ct = default)
    {
        try
        {
            var state = _internetCat.State;
            var result = await _internetCat.PollAsync(ct);

            return Ok(new
            {
                status = state.ToString(),
                connected = state.ToString() == "Connected",
                frequency = result?.FrequencyMhz,
                mode = result?.MappedMode,
                subMode = result?.SubMode,
                band = result?.Band,
                power = result?.PowerWatts
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error polling CAT status");
            return StatusCode(500, new { error = "Failed to poll CAT status" });
        }
    }

    [HttpGet("config")]
    public ActionResult GetConfig()
    {
        try
        {
            return Ok(new
            {
                enabled = _settings.InternetRadioEnabled,
                host = _settings.InternetRadioHost,
                port = _settings.InternetRadioPort
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching CAT config");
            return StatusCode(500, new { error = "Failed to fetch CAT config" });
        }
    }

    [HttpPost("config")]
    public ActionResult SetConfig([FromBody] CatConfigRequest config)
    {
        try
        {
            if (config == null)
                return BadRequest(new { error = "Configuration required" });

            _settings.InternetRadioEnabled = config.Enabled;
            _settings.InternetRadioHost = config.Host;
            _settings.InternetRadioPort = config.Port;

            return Ok(new { message = "CAT configuration updated" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating CAT config");
            return StatusCode(500, new { error = "Failed to update CAT config" });
        }
    }
}

public class CatConfigRequest
{
    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 9200;
}
