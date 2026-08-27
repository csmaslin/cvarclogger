namespace CvarcLogger.Core.Geo;

/// <summary>Resolves CQ/ITU zone from a Maidenhead grid square. Grid-based resolution is more precise
/// than a DXCC entity's nominal zone (see ICallsignEntityResolver) for split-zone countries — the USA
/// alone spans CQ zones 3-8 depending on where in the country a station actually is.</summary>
public interface IGridZoneResolver
{
    (int? CqZone, int? ItuZone) Resolve(string? gridSquare);
}
