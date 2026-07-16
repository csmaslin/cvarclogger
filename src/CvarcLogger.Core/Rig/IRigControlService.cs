namespace CvarcLogger.Core.Rig;

public enum RigConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>Result of a connect attempt. Never throws — mirrors CallsignLookupResult's
/// Found/Error pattern for best-effort external I/O.</summary>
public record RigConnectResult(bool Success, string? Error = null)
{
    public static RigConnectResult Ok() => new(true);
    public static RigConnectResult Failed(string error) => new(false, error);
}

/// <summary>Result of one poll cycle (frequency + mode). FrequencyMhz matches Qso.FrequencyMhz's
/// unit (decimal MHz). RawMode is the untranslated rigctld mode string; MappedMode is the
/// CvarcLogger Modes-list value; Band is the derived band, or null if outside all recognized bands.</summary>
public record RigPollResult(bool Success, decimal? FrequencyMhz = null, string? RawMode = null,
    string? MappedMode = null, string? Band = null, string? Error = null)
{
    public static RigPollResult Ok(decimal freqMhz, string rawMode, string mappedMode, string? band) =>
        new(true, freqMhz, rawMode, mappedMode, band);
    public static RigPollResult Failed(string error) => new(false, Error: error);
}

/// <summary>TCP client of a running rigctld instance (Hamlib). Never throws for connection or
/// protocol failures — all failure modes surface as a false Success flag with an Error message,
/// so a UI poll loop never needs try/catch to stay safe.</summary>
public interface IRigControlService : IAsyncDisposable
{
    RigConnectionState State { get; }
    Task<RigConnectResult> ConnectAsync(string host, int port, CancellationToken ct = default);
    Task DisconnectAsync();
    Task<RigPollResult> PollAsync(CancellationToken ct = default);
}
