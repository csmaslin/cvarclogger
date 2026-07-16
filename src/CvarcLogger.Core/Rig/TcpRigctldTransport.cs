using System.Net.Sockets;
using System.Text;

namespace CvarcLogger.Core.Rig;

public class TcpRigctldTransport : IRigctldTransport
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public bool IsConnected => _client is { Connected: true };

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        Disconnect();

        _client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        await _client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);

        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.ASCII);
        _writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };
    }

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        if (_writer is null) throw new InvalidOperationException("Not connected.");
        await _writer.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
    }

    public async Task<string> ReadLineAsync(CancellationToken ct = default)
    {
        if (_reader is null) throw new InvalidOperationException("Not connected.");
        string? line = await _reader.ReadLineAsync(ct).ConfigureAwait(false);
        return line ?? throw new IOException("Connection closed by rigctld.");
    }

    public void Disconnect()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();
        _reader = null;
        _writer = null;
        _client = null;
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}
