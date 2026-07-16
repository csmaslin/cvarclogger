namespace CvarcLogger.Core.Awards;

/// <summary>Optional narrowing for awards computation, matching how N1MM/Log4OM users track per-band/mode DXCC.</summary>
public record AwardsFilter(string? Band = null, string? Mode = null);
