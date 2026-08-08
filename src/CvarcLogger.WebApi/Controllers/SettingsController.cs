using Microsoft.AspNetCore.Mvc;
using CvarcLogger.App.Services;
using Serilog;

namespace CvarcLogger.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settings;

    public SettingsController(SettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet("column-visibility")]
    public ActionResult GetColumnVisibility()
    {
        try
        {
            var hiddenColumns = _settings.HiddenLogColumns;
            return Ok(new { hiddenColumns = hiddenColumns?.ToList() ?? new List<string>() });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching column visibility");
            return StatusCode(500, new { error = "Failed to fetch column visibility" });
        }
    }

    [HttpPost("column-visibility")]
    public ActionResult SetColumnVisibility([FromBody] ColumnVisibilityRequest request)
    {
        try
        {
            if (request == null || request.HiddenColumns == null)
                return BadRequest(new { error = "Hidden columns list required" });

            // Update hidden columns
            _settings.HiddenLogColumns.Clear();
            foreach (var column in request.HiddenColumns)
            {
                _settings.HiddenLogColumns.Add(column);
            }
            _settings.SaveHiddenLogColumns();

            return Ok(new { message = "Column visibility updated" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating column visibility");
            return StatusCode(500, new { error = "Failed to update column visibility" });
        }
    }

    [HttpGet("column-order")]
    public ActionResult GetColumnOrder()
    {
        try
        {
            var order = _settings.LogColumnOrder;
            return Ok(new { columnOrder = order });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching column order");
            return StatusCode(500, new { error = "Failed to fetch column order" });
        }
    }

    [HttpPost("column-order")]
    public ActionResult SetColumnOrder([FromBody] ColumnOrderRequest request)
    {
        try
        {
            if (request == null || request.ColumnOrder == null)
                return BadRequest(new { error = "Column order required" });

            _settings.SaveLogColumnOrder(request.ColumnOrder);
            return Ok(new { message = "Column order updated" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating column order");
            return StatusCode(500, new { error = "Failed to update column order" });
        }
    }

    [HttpGet("column-widths")]
    public ActionResult GetColumnWidths()
    {
        try
        {
            var widths = _settings.LogColumnWidths;
            return Ok(new { columnWidths = widths });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching column widths");
            return StatusCode(500, new { error = "Failed to fetch column widths" });
        }
    }

    [HttpPost("column-widths")]
    public ActionResult SetColumnWidths([FromBody] ColumnWidthsRequest request)
    {
        try
        {
            if (request == null || request.ColumnWidths == null)
                return BadRequest(new { error = "Column widths required" });

            _settings.SaveLogColumnWidths(request.ColumnWidths);
            return Ok(new { message = "Column widths updated" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating column widths");
            return StatusCode(500, new { error = "Failed to update column widths" });
        }
    }

    [HttpGet("station/default")]
    public ActionResult GetDefaultStationId()
    {
        try
        {
            return Ok(new { stationProfileId = _settings.LastUsedStationProfileId });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching default station");
            return StatusCode(500, new { error = "Failed to fetch default station" });
        }
    }

    [HttpPost("station/default")]
    public ActionResult SetDefaultStation([FromBody] DefaultStationRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Station profile ID required" });

            _settings.LastUsedStationProfileId = request.StationProfileId;
            return Ok(new { message = "Default station updated" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting default station");
            return StatusCode(500, new { error = "Failed to set default station" });
        }
    }
}

public class ColumnVisibilityRequest
{
    public List<string>? HiddenColumns { get; set; }
}

public class ColumnOrderRequest
{
    public Dictionary<string, int>? ColumnOrder { get; set; }
}

public class ColumnWidthsRequest
{
    public Dictionary<string, double>? ColumnWidths { get; set; }
}

public class DefaultStationRequest
{
    public int? StationProfileId { get; set; }
}
