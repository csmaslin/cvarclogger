using System;
using System.Collections.Generic;

namespace CvarcLogger.Data.Models;

public partial class StationProfile
{
    public int Id { get; set; }

    public string Callsign { get; set; } = null!;

    public string? OperatorCallsign { get; set; }

    public string? MyGridSquare { get; set; }

    public string? MyState { get; set; }

    public string? MyCounty { get; set; }

    public int IsDefault { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<Qso> Qsos { get; set; } = new List<Qso>();
}
