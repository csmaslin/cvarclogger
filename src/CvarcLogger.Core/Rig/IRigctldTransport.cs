namespace CvarcLogger.Core.Rig;

/// <summary>Line-oriented transport over a rigctld TCP session. RigctldClient's command
/// orchestration and response parsing is unit-tested against a fake implementation of this
/// interface; TcpRigctldTransport is the only piece that touches a real socket.</summary>
public interface IRigctldTransport : IAsyncDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(string host, int port, CancellationToken ct = default);
    Task WriteLineAsync(string line, CancellationToken ct = default);
    Task<string> ReadLineAsync(CancellationToken ct = default);
    void Disconnect();
}
