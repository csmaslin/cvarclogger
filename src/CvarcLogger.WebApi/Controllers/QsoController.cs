using Microsoft.AspNetCore.Mvc;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;
using Serilog;

namespace CvarcLogger.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QsoController : ControllerBase
{
    private readonly IQsoRepository _qsoRepository;

    public QsoController(IQsoRepository qsoRepository)
    {
        _qsoRepository = qsoRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<Qso>>> GetAll(CancellationToken ct = default)
    {
        try
        {
            var qsos = await _qsoRepository.GetAllAsync(ct);
            return Ok(qsos);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching QSOs");
            return StatusCode(500, new { error = "Failed to fetch QSOs" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Qso>> GetById(int id, CancellationToken ct = default)
    {
        try
        {
            var qso = await _qsoRepository.GetByIdAsync(id, ct);
            if (qso == null)
                return NotFound(new { error = "QSO not found" });
            return Ok(qso);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching QSO {Id}", id);
            return StatusCode(500, new { error = "Failed to fetch QSO" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<Qso>> Create([FromBody] Qso qso, CancellationToken ct = default)
    {
        try
        {
            if (qso == null)
                return BadRequest(new { error = "QSO data required" });

            var created = await _qsoRepository.AddAsync(qso, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating QSO");
            return StatusCode(500, new { error = "Failed to create QSO" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] Qso qso, CancellationToken ct = default)
    {
        try
        {
            if (qso == null)
                return BadRequest(new { error = "QSO data required" });

            qso.Id = id;
            await _qsoRepository.UpdateAsync(qso, ct);
            return Ok(new { message = "QSO updated successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating QSO {Id}", id);
            return StatusCode(500, new { error = "Failed to update QSO" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct = default)
    {
        try
        {
            await _qsoRepository.DeleteAsync(id, ct);
            return Ok(new { message = "QSO deleted successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting QSO {Id}", id);
            return StatusCode(500, new { error = "Failed to delete QSO" });
        }
    }

    [HttpDelete("clear-all")]
    public async Task<ActionResult> DeleteAll(CancellationToken ct = default)
    {
        try
        {
            var count = await _qsoRepository.DeleteAllAsync(ct);
            return Ok(new { message = $"Deleted {count} QSOs" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clearing QSOs");
            return StatusCode(500, new { error = "Failed to clear QSOs" });
        }
    }
}
