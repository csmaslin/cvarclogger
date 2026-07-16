namespace CvarcLogger.Core.Models;

/// <summary>A DXCC "entity" (roughly, a country/territory as defined by the ARRL DXCC award program).</summary>
public class DxccEntity
{
    /// <summary>The ARRL DXCC entity number (e.g. 291 = USA, 6 = Alaska, 110 = Hawaii). Used as the primary key since it's the canonical stable identifier.</summary>
    public int EntityCode { get; set; }

    public string EntityName { get; set; } = string.Empty;
    public string? Continent { get; set; }
    public int? CqZone { get; set; }
    public int? ItuZone { get; set; }

    /// <summary>True if the ARRL has deleted this entity (still loggable historically, but excluded from "current" DXCC totals).</summary>
    public bool IsDeleted { get; set; }

    public string? Notes { get; set; }

    public List<PrefixMapping> Prefixes { get; set; } = new();
}
