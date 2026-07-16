namespace CvarcLogger.Core.Models;

/// <summary>Maps a callsign prefix (e.g. "KH6", "KL", "K") to a DXCC entity. Resolution walks prefixes longest-first.</summary>
public class PrefixMapping
{
    public int Id { get; set; }

    public string Prefix { get; set; } = string.Empty;

    public int DxccEntityCode { get; set; }
    public DxccEntity? DxccEntity { get; set; }
}
