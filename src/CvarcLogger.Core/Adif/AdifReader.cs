namespace CvarcLogger.Core.Adif;

/// <summary>
/// Minimal ADIF v3 reader. Parses length-prefixed tags (&lt;FIELD:LEN[:TYPE]&gt;value) rather than
/// splitting on lines, so multi-line values (e.g. COMMENT/NOTES with embedded newlines) parse correctly.
/// A file is headerless if its first tag is &lt;EOR&gt; before any &lt;EOH&gt; is seen; either way, whatever
/// tags precede the first &lt;EOH&gt; are discarded as header fields, and tags between &lt;EOH&gt;/start and each
/// &lt;EOR&gt; become one record.
/// </summary>
public static class AdifReader
{
    public static List<AdifRecord> ReadAll(TextReader reader)
    {
        string content = reader.ReadToEnd();
        return ReadAll(content);
    }

    public static List<AdifRecord> ReadAll(string content)
    {
        var records = new List<AdifRecord>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int i = 0;
        int len = content.Length;
        while (i < len)
        {
            if (content[i] != '<')
            {
                i++; // free text between tags (header preamble, whitespace) is ignored
                continue;
            }

            int close = content.IndexOf('>', i + 1);
            if (close < 0)
            {
                break; // malformed trailing tag — stop parsing
            }

            string tagContent = content.Substring(i + 1, close - i - 1);
            string[] parts = tagContent.Split(':');
            string name = parts[0].Trim();
            i = close + 1;

            if (name.Equals("EOR", StringComparison.OrdinalIgnoreCase))
            {
                records.Add(new AdifRecord(current));
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (name.Equals("EOH", StringComparison.OrdinalIgnoreCase))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (parts.Length >= 2 && int.TryParse(parts[1], out int valueLength) && valueLength > 0)
            {
                int available = Math.Min(valueLength, len - i);
                string value = content.Substring(i, available);
                i += available;
                current[name] = value;
            }
            else
            {
                current[name] = string.Empty;
            }
        }

        return records;
    }
}
