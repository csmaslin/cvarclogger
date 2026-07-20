using System.Globalization;
using System.Net.Sockets;

namespace CvarcLogger.Core.Rig;

/// <summary>rigctld TCP protocol client. Protocol facts: `f\n` returns one line, the frequency in
/// Hz as a decimal string (e.g. "14250000.000000"); `m\n` returns two lines, the mode name (e.g.
/// "USB") then the passband in Hz (currently unused here); `l RFPOWER\n` returns one line, the rig's
/// power-control level as a 0.0-1.0 fraction; any line starting with "RPRT" followed by a negative
/// number is an error response.</summary>
public class RigctldClient : IRigControlService
{
    private readonly IRigctldTransport _transport;

    public RigctldClient(IRigctldTransport? transport = null)
    {
        _transport = transport ?? new TcpRigctldTransport();
    }

    public RigConnectionState State { get; private set; } = RigConnectionState.Disconnected;

    public async Task<RigConnectResult> ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        State = RigConnectionState.Connecting;
        try
        {
            await _transport.ConnectAsync(host, port, ct).ConfigureAwait(false);
            State = RigConnectionState.Connected;
            return RigConnectResult.Ok();
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            State = RigConnectionState.Error;
            return RigConnectResult.Failed(ex.Message);
        }
    }

    public async Task<RigPollResult> PollAsync(CancellationToken ct = default)
    {
        if (State != RigConnectionState.Connected)
            return RigPollResult.Failed("Not connected.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            await _transport.WriteLineAsync("f", timeoutCts.Token).ConfigureAwait(false);
            string freqLine = await _transport.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            if (TryGetRprtError(freqLine, out var freqError))
                return RigPollResult.Failed(freqError);
            if (!decimal.TryParse(freqLine.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var freqHz))
                return RigPollResult.Failed($"Unexpected frequency response: '{freqLine}'");

            await _transport.WriteLineAsync("m", timeoutCts.Token).ConfigureAwait(false);
            string modeLine = await _transport.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            if (TryGetRprtError(modeLine, out var modeError))
                return RigPollResult.Failed(modeError);
            _ = await _transport.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false); // passband line, currently unused

            // RFPOWER is a "get_level" query, not universally supported -- an RPRT error or unparsable
            // response here just means this rig doesn't expose it over CAT, not a poll failure, since
            // frequency/mode (the two things every rig supports) already succeeded above.
            decimal? powerFraction = null;
            await _transport.WriteLineAsync("l RFPOWER", timeoutCts.Token).ConfigureAwait(false);
            string powerLine = await _transport.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            if (!TryGetRprtError(powerLine, out _) &&
                decimal.TryParse(powerLine.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFraction))
            {
                powerFraction = parsedFraction;
            }

            decimal freqMhz = freqHz / 1_000_000m;
            string rawMode = modeLine.Trim();
            string mappedMode = RigModeMapper.ToCvarcLoggerMode(rawMode);
            string? subMode = RigModeMapper.ToCvarcLoggerSubMode(rawMode);
            return RigPollResult.Ok(freqMhz, rawMode, mappedMode, subMode, BandCalculator.FromFrequencyMhz(freqMhz), powerFraction);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            State = RigConnectionState.Error;
            return RigPollResult.Failed(ex.Message);
        }
    }

    public async Task DisconnectAsync()
    {
        _transport.Disconnect();
        State = RigConnectionState.Disconnected;
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _transport.DisposeAsync();

    private static bool TryGetRprtError(string line, out string error)
    {
        if (line.StartsWith("RPRT", StringComparison.OrdinalIgnoreCase))
        {
            error = $"rigctld error: {line}";
            return true;
        }
        error = string.Empty;
        return false;
    }
}
