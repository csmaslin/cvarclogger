using Microsoft.AspNetCore.Mvc;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using Serilog;

namespace CvarcLogger.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StationController : ControllerBase
{
    private readonly IStationProfileRepository _stationRepository;

    public StationController(IStationProfileRepository stationRepository)
    {
        _stationRepository = stationRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<StationProfile>>> GetAll(CancellationToken ct = default)
    {
        try
        {
            var profiles = await _stationRepository.GetAllAsync(ct);
            return Ok(profiles);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching station profiles");
            return StatusCode(500, new { error = "Failed to fetch station profiles" });
        }
    }

    [HttpGet("default")]
    public async Task<ActionResult<StationProfile>> GetDefault(CancellationToken ct = default)
    {
        try
        {
            var profile = await _stationRepository.GetDefaultAsync(ct);
            if (profile == null)
                return NotFound(new { error = "No default station profile configured" });
            return Ok(profile);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching default station profile");
            return StatusCode(500, new { error = "Failed to fetch default station profile" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<StationProfile>> Create([FromBody] StationProfile profile, CancellationToken ct = default)
    {
        try
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Callsign))
                return BadRequest(new { error = "Callsign is required" });

            var created = await _stationRepository.AddAsync(profile, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating station profile");
            return StatusCode(500, new { error = "Failed to create station profile" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StationProfile>> GetById(int id, CancellationToken ct = default)
    {
        try
        {
            var profiles = await _stationRepository.GetAllAsync(ct);
            var profile = profiles.FirstOrDefault(p => p.Id == id);
            if (profile == null)
                return NotFound(new { error = "Station profile not found" });
            return Ok(profile);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching station profile {Id}", id);
            return StatusCode(500, new { error = "Failed to fetch station profile" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] StationProfile profile, CancellationToken ct = default)
    {
        try
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Callsign))
                return BadRequest(new { error = "Callsign is required" });

            profile.Id = id;
            await _stationRepository.UpdateAsync(profile, ct);
            return Ok(new { message = "Station profile updated successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating station profile {Id}", id);
            return StatusCode(500, new { error = "Failed to update station profile" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct = default)
    {
        try
        {
            await _stationRepository.DeleteAsync(id, ct);
            return Ok(new { message = "Station profile deleted successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting station profile {Id}", id);
            return StatusCode(500, new { error = "Failed to delete station profile" });
        }
    }
}
