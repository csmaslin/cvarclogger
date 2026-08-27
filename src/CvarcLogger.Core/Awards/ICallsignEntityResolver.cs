using CvarcLogger.Core.Models;

namespace CvarcLogger.Core.Awards;

/// <summary>Resolves a callsign to a DXCC entity via longest-prefix match against the bundled prefix table.</summary>
public interface ICallsignEntityResolver
{
    Task<DxccEntity?> ResolveAsync(string callsign, CancellationToken ct = default);
}
