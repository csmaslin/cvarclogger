using System.IO;
using System.Net.Sockets;
using System.Text;
using CvarcLogger.Core.Adif;
using CvarcLogger.Core.Models;
using Serilog;

namespace CvarcLogger.App.Services;

/// <summary>Broadcasts each logged/edited QSO as a single ADIF record over UDP so companion apps like
/// GridTracker2 can plot it on their map/grid-tracking in real time. This mirrors how N1MM+, DXKeeper,
/// Log4OM etc. feed GridTracker: just the QSO's ADIF fields followed by &lt;EOR&gt;, no ADIF header,
/// sent to whatever host/port GridTracker2's "Receive ADIF UDP (Broadcasts for Logging)" setting
/// (General tab) is configured to -- 127.0.0.1:2240 by default here, confirmed working 2026-07-19 (not
/// GridTracker2's own generic 2333 listener, since 2333/2237 are already spoken for by GridTracker2's
/// direct WSJT-X capture, see WsjtxUdpListenerService).</summary>
public class GridTrackerBroadcastService : IDisposable
{
    private readonly SettingsService _settings;
    private UdpClient? _client;

    public GridTrackerBroadcastService(SettingsService settings)
    {
        _settings = settings;
    }

    public void BroadcastQso(Qso qso)
    {
        if (!_settings.GridTrackerEnabled) return;
        Send(AdifFieldMapper.ToAdifRecord(qso));
    }

    /// <summary>Sends a fixed, recognizable test QSO regardless of whether broadcasting is currently
    /// enabled, so the user can confirm GridTracker2 is receiving packets before turning the feature on.</summary>
    public void SendTestPacket()
    {
        Send(AdifFieldMapper.ToAdifRecord(new Qso
        {
            Callsign = "W1AW",
            Band = "20m",
            Mode = "SSB",
            QsoDateTimeOnUtc = DateTime.UtcNow,
            GridSquare = "FN31pr",
            StationCallsign = "TEST",
        }));
    }

    private void Send(AdifRecord record)
    {
        try
        {
            using var writer = new StringWriter();
            AdifWriter.WriteRecord(writer, record);
            byte[] bytes = Encoding.UTF8.GetBytes(writer.ToString());

            _client ??= new UdpClient();
            _client.Send(bytes, bytes.Length, _settings.GridTrackerHost, _settings.GridTrackerPort);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to broadcast QSO to GridTracker2.");
        }
    }

    public void Dispose() => _client?.Dispose();
}
