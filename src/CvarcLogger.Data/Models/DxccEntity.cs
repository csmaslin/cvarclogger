using System;
using System.Collections.Generic;

namespace CvarcLogger.Data.Models;

public partial class DxccEntity
{
    public int EntityCode { get; set; }

    public string EntityName { get; set; } = null!;

    public string? Continent { get; set; }

    public int? CqZone { get; set; }

    public int? ItuZone { get; set; }

    public int IsDeleted { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<PrefixMapping> PrefixMappings { get; set; } = new List<PrefixMapping>();

    public virtual ICollection<Qso> Qsos { get; set; } = new List<Qso>();
}
