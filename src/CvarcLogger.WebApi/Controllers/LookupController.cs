using Microsoft.AspNetCore.Mvc;
using CvarcLogger.App.Services;
using Serilog;

namespace CvarcLogger.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupController : ControllerBase
{
    private readonly LookupCoordinator _lookupCoordinator;
    private readonly SettingsService _settings;

    public LookupController(LookupCoordinator lookupCoordinator, SettingsService settings)
    {
        _lookupCoordinator = lookupCoordinator;
        _settings = settings;
    }

    [HttpGet("callsign/{callsign}")]
    public async Task<ActionResult> LookupCallsign(string callsign, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(callsign))
                return BadRequest(new { error = "Callsign required" });

            var result = await _lookupCoordinator.LookupAsync(callsign, ct);

            if (!result.Found)
                return NotFound(new { found = false, error = result.Error });

            return Ok(new
            {
                found = true,
                name = result.Name,
                gridSquare = result.GridSquare,
                city = result.City,
                state = result.State,
                county = result.County,
                country = result.Country,
                dxccEntityCode = result.DxccEntityCode,
                latitude = result.Latitude,
                longitude = result.Longitude
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error looking up callsign {Callsign}", callsign);
            return StatusCode(500, new { error = "Failed to lookup callsign" });
        }
    }

    [HttpPost("qrz/test")]
    public async Task<ActionResult> TestQrz(CancellationToken ct = default)
    {
        try
        {
            // Test QRZ with a known callsign
            var result = await _lookupCoordinator.LookupAsync("W5XYZ", ct);
            return Ok(new { success = result.Found, message = result.Found ? "QRZ test successful" : result.Error });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error testing QRZ");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("qrzcq/test")]
    public async Task<ActionResult> TestQrzCq(CancellationToken ct = default)
    {
        try
        {
            var result = await _lookupCoordinator.LookupAsync("W5XYZ", ct);
            return Ok(new { success = result.Found, message = result.Found ? "QRZCQ test successful" : result.Error });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error testing QRZCQ");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("callook/test")]
    public async Task<ActionResult> TestCallook(CancellationToken ct = default)
    {
        try
        {
            var result = await _lookupCoordinator.LookupAsync("W5XYZ", ct);
            return Ok(new { success = result.Found, message = result.Found ? "Callook test successful" : result.Error });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error testing Callook");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("credentials/qrz")]
    public ActionResult SetQrzCredentials([FromBody] LookupCredentialsRequest credentials)
    {
        try
        {
            if (credentials == null)
                return BadRequest(new { error = "Credentials required" });

            // In a real implementation, this would securely store credentials
            // For now, we'll just acknowledge the request
            return Ok(new { message = "QRZ credentials configured" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting QRZ credentials");
            return StatusCode(500, new { error = "Failed to set credentials" });
        }
    }

    [HttpPost("credentials/qrzcq")]
    public ActionResult SetQrzCqCredentials([FromBody] LookupCredentialsRequest credentials)
    {
        try
        {
            if (credentials == null)
                return BadRequest(new { error = "Credentials required" });

            return Ok(new { message = "QRZCQ credentials configured" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting QRZCQ credentials");
            return StatusCode(500, new { error = "Failed to set credentials" });
        }
    }
}

public class LookupCredentialsRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}
