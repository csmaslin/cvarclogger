using System;
using System.Collections.Generic;

namespace CvarcLogger.Data.Models;

public partial class PrefixMapping
{
    public int Id { get; set; }

    public string Prefix { get; set; } = null!;

    public int DxccEntityCode { get; set; }

    public virtual DxccEntity DxccEntityCodeNavigation { get; set; } = null!;
}
