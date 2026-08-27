using System.IO;
using System.Net.Sockets;
using System.Text;
using CvarcLogger.Core.Rig;

namespace CvarcLogger.App.Services;

/// <summary>Raw TCP client for Elecraft K4's native CAT command language (not Hamlib rigctld -- a
/// completely different wire protocol) -- connects directly to the radio's Ethernet interface (default
/// port 9200) over the local network or the internet, sends semicolon-terminated ASCII commands, and reads
/// until a semicolon appears in the response. Read-only status queries only (FA/MD); this client never
/// sends anything that changes the radio's state. Confirmed against Elecraft's official K4 Programmer's
/// Reference, rev. C10. Ported from the companion CvarcCellLog app.</summary>
public sealed class K4CatClient : IAsyncDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    // Bytes read from the socket but not yet consumed as a complete ';'-terminated reply -- the radio can
    // batch more than one reply into a single TCP packet, so leftovers here carry over to the next call.
    private readonly StringBuilder _pending = new();

    public K4ConnectionState State { get; private set; } = K4ConnectionState.Disconnected;

    public async Task<K4ConnectResult> ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        Disconnect();
        State = K4ConnectionState.Connecting;
        try
        {
            _client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5)); // generous for internet/VPN, not just LAN
            await _client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            _stream = _client.GetStream();

            // Enables "advanced K4 mode" -- a SET-form command (a value is included), so per Kenwood/
            // Elecraft convention it produces no reply to read, matching the user's own working example.
            await SendAsync("K41", timeoutCts.Token).ConfigureAwait(false);

            State = K4ConnectionState.Connected;
            return K4ConnectResult.Ok();
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            State = K4ConnectionState.Error;
            Disconnect();
            return K4ConnectResult.Failed(ex.Message);
        }
    }

    public async Task<K4PollResult> PollAsync(CancellationToken ct = default)
    {
        if (State != K4ConnectionState.Connected)
            return K4PollResult.Failed("Not connected.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            string faReply = await QueryAsync("FA", timeoutCts.Token).ConfigureAwait(false);
            if (!K4ReplyParser.TryParseFrequencyHz(faReply, out var freqHz))
                return K4PollResult.Failed($"Unexpected FA reply: '{faReply}'");

            string mdReply = await QueryAsync("MD", timeoutCts.Token).ConfigureAwait(false);
            string? mappedMode = null, subMode = null;
            if (K4ReplyParser.TryParseModeDigit(mdReply, out var digit))
            {
                string? rigctldMode = K4ModeAdapter.ToRigctldModeString(digit);
                if (rigctldMode is not null)
                {
                    mappedMode = RigModeMapper.ToCvarcLoggerMode(rigctldMode);
                    subMode = RigModeMapper.ToCvarcLoggerSubMode(rigctldMode);
                }
            }

            // PCX; (rather than plain PC;) always returns the full "PCnnnr" K4-format reply, range letter
            // included, regardless of any legacy K3-format meta-mode in effect -- see K4ReplyParser.
            string pcReply = await QueryAsync("PCX", timeoutCts.Token).ConfigureAwait(false);
            decimal? powerWatts = K4ReplyParser.TryParsePowerWatts(pcReply, out var watts) ? watts : null;

            decimal freqMhz = freqHz / 1_000_000m;
            return K4PollResult.Ok(freqMhz, mappedMode, subMode, BandCalculator.FromFrequencyMhz(freqMhz), powerWatts);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            State = K4ConnectionState.Error;
            return K4PollResult.Failed(ex.Message);
        }
    }

    public Task DisconnectAsync()
    {
        Disconnect();
        State = K4ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Disconnect();
        await Task.CompletedTask;
    }

    private async Task<string> QueryAsync(string command, CancellationToken ct)
    {
        await SendAsync(command, ct).ConfigureAwait(false);
        return await ReadReplyAsync(ct).ConfigureAwait(false);
    }

    private async Task SendAsync(string command, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected.");
        byte[] bytes = Encoding.ASCII.GetBytes(command + ";");
        await _stream.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    private async Task<string> ReadReplyAsync(CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected.");
        var buffer = new byte[256];
        while (true)
        {
            string current = _pending.ToString();
            int idx = current.IndexOf(';');
            if (idx >= 0)
            {
                _pending.Remove(0, idx + 1);
                return current[..(idx + 1)];
            }

            int read = await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0) throw new IOException("Connection closed by radio.");
            _pending.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
    }

    private void Disconnect()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        _pending.Clear();
    }
}
