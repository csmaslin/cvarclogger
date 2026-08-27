using System.Text;

namespace CvarcLogger.Core.Adif;

public static class AdifWriter
{
    public static void WriteHeader(TextWriter writer)
    {
        WriteTag(writer, "ADIF_VER", "3.1.4");
        WriteTag(writer, "PROGRAMID", "CvarcLogger");
        writer.Write("<EOH>\r\n");
    }

    public static void WriteRecord(TextWriter writer, AdifRecord record)
    {
        foreach (var field in record.Fields)
        {
            WriteTag(writer, field.Key, field.Value);
        }
        writer.Write("<EOR>\r\n");
    }

    public static void WriteAll(TextWriter writer, IEnumerable<AdifRecord> records)
    {
        WriteHeader(writer);
        foreach (var record in records)
        {
            WriteRecord(writer, record);
        }
    }

    /// <summary>ADIF's &lt;FIELD:LENGTH&gt; length is defined in bytes, not .NET characters -- matters for
    /// any non-ASCII UTF-8 content (e.g. an accented name), where a character can take 2-4 bytes. Using
    /// value.Length (a UTF-16 code unit count) would under-count for such values and desync every field
    /// that follows for any ADIF reader that treats the length as bytes -- the de facto convention across
    /// the ADIF ecosystem (WSJT-X, QRZ, N1MM, DXKeeper, etc.), even though ADIF 3.1.4's own String data
    /// type is nominally ASCII-only.</summary>
    private static void WriteTag(TextWriter writer, string name, string value)
    {
        int byteLength = Encoding.UTF8.GetByteCount(value);
        writer.Write($"<{name.ToUpperInvariant()}:{byteLength}>{value}");
    }
}
