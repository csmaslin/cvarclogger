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

    private static void WriteTag(TextWriter writer, string name, string value)
    {
        writer.Write($"<{name.ToUpperInvariant()}:{value.Length}>{value}");
    }
}
