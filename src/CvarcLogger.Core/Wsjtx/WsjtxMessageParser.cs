using System.Text;

namespace CvarcLogger.Core.Wsjtx;

/// <summary>Parses WSJT-X's UDP broadcast protocol (default port 2237), the same wire format used by
/// GridTracker, JTAlert, N1MM+, etc. Only message type 12 ("Logged ADIF", broadcast once each time
/// WSJT-X logs a QSO to its own log) is handled -- everything else (Heartbeat, Status, Decode, ...)
/// is silently ignored, since it's frequent chatter on the same port we don't need.</summary>
public static class WsjtxMessageParser
{
    private const uint MagicNumber = 0xadbccbda;
    private const uint LoggedAdifMessageType = 12;

    /// <summary>Extracts the ADIF record text from a WSJT-X "Logged ADIF" datagram, or null if this
    /// datagram isn't that message type or is malformed/truncated. Wire format: big-endian integers;
    /// strings are a big-endian uint32 byte-length prefix followed by that many UTF-8 bytes
    /// (0xFFFFFFFF means a null/absent optional string, not used by this message's fields).</summary>
    public static string? TryExtractLoggedAdif(byte[] datagram)
    {
        try
        {
            int pos = 0;
            uint magic = ReadUInt32(datagram, ref pos);
            if (magic != MagicNumber) return null;

            _ = ReadUInt32(datagram, ref pos); // schema version -- this message's shape is stable across versions
            uint messageType = ReadUInt32(datagram, ref pos);
            if (messageType != LoggedAdifMessageType) return null;

            _ = ReadString(datagram, ref pos); // Id (WSJT-X instance name) -- unused
            return ReadString(datagram, ref pos); // ADIF text
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return null; // truncated/malformed datagram -- not ours to worry about
        }
    }

    private static uint ReadUInt32(byte[] data, ref int pos)
    {
        if (pos + 4 > data.Length) throw new IndexOutOfRangeException();
        uint value = (uint)((data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3]);
        pos += 4;
        return value;
    }

    private static string? ReadString(byte[] data, ref int pos)
    {
        uint length = ReadUInt32(data, ref pos);
        if (length == 0xFFFFFFFF) return null;
        if (pos + length > data.Length) throw new IndexOutOfRangeException();
        string value = Encoding.UTF8.GetString(data, pos, (int)length);
        pos += (int)length;
        return value;
    }
}
