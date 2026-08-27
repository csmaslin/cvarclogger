using Microsoft.AspNetCore.Mvc;
using CvarcLogger.Core.Abstractions;
using Serilog;

namespace CvarcLogger.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReferenceDataController : ControllerBase
{
    private readonly IDxccEntityRepository _dxccRepository;
    private readonly ISotaActivationRepository _sotaRepository;
    private readonly IPotaActivationRepository _potaRepository;

    public ReferenceDataController(
        IDxccEntityRepository dxccRepository,
        ISotaActivationRepository sotaRepository,
        IPotaActivationRepository potaRepository)
    {
        _dxccRepository = dxccRepository;
        _sotaRepository = sotaRepository;
        _potaRepository = potaRepository;
    }

    [HttpGet("dxcc")]
    public async Task<ActionResult> GetDxccEntities(CancellationToken ct = default)
    {
        try
        {
            var entities = await _dxccRepository.GetAllWithPrefixesAsync(ct);
            return Ok(new { count = entities.Count, entities });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching DXCC entities");
            return StatusCode(500, new { error = "Failed to fetch DXCC entities" });
        }
    }

    [HttpGet("dxcc/search")]
    public async Task<ActionResult> SearchDxcc([FromQuery] string query, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { error = "Search query required" });

            var entities = await _dxccRepository.GetAllWithPrefixesAsync(ct);
            var results = entities
                .Where(e => e.EntityName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                           e.Continent?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .Take(20)
                .ToList();

            return Ok(new { count = results.Count, results });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching DXCC entities");
            return StatusCode(500, new { error = "Failed to search DXCC entities" });
        }
    }

    [HttpGet("sota")]
    public async Task<ActionResult> GetSotaSummits(CancellationToken ct = default)
    {
        try
        {
            var summits = await _sotaRepository.GetAllAsync(ct);
            return Ok(new { count = summits.Count, summits = summits.Take(100).ToList() });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching SOTA summits");
            return StatusCode(500, new { error = "Failed to fetch SOTA summits" });
        }
    }

    [HttpGet("sota/search")]
    public async Task<ActionResult> SearchSota([FromQuery] string query, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { error = "Search query required" });

            var summits = await _sotaRepository.GetAllAsync(ct);
            var results = summits
                .Where(s => s.SummitCode?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                           s.SummitName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .Take(20)
                .ToList();

            return Ok(new { count = results.Count, results });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching SOTA summits");
            return StatusCode(500, new { error = "Failed to search SOTA summits" });
        }
    }

    [HttpGet("pota")]
    public async Task<ActionResult> GetPotaParks(CancellationToken ct = default)
    {
        try
        {
            var parks = await _potaRepository.GetAllAsync(ct);
            return Ok(new { count = parks.Count, parks = parks.Take(100).ToList() });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching POTA parks");
            return StatusCode(500, new { error = "Failed to fetch POTA parks" });
        }
    }

    [HttpGet("pota/search")]
    public async Task<ActionResult> SearchPota([FromQuery] string query, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { error = "Search query required" });

            var parks = await _potaRepository.GetAllAsync(ct);
            var results = parks
                .Where(p => p.ParkReference?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                           p.ParkName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .Take(20)
                .ToList();

            return Ok(new { count = results.Count, results });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching POTA parks");
            return StatusCode(500, new { error = "Failed to search POTA parks" });
        }
    }
}
