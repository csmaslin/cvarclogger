using System.Text;

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

    public static List<AdifRecord> ReadAll(string content) => ReadAll(Encoding.UTF8.GetBytes(content));

    /// <summary>Reads a file's raw bytes directly rather than through a pre-decoded string -- prefer
    /// this for importing real files, since decoding the whole file as text first (e.g. via
    /// StreamReader) would already have thrown away the byte-level information ReadAll(byte[]) needs to
    /// recover from a non-UTF-8 source (see its doc comment).</summary>
    public static List<AdifRecord> ReadAllFromFile(string path) => ReadAll(File.ReadAllBytes(path));

    /// <summary>Parses directly from raw bytes, tracking position purely in bytes throughout -- ADIF's
    /// &lt;FIELD:LEN&gt; length is a byte count (see AdifWriter), so this sidesteps any char-vs-byte
    /// accounting drift entirely rather than trying to reconstruct byte offsets from an
    /// already-decoded .NET string.
    ///
    /// Each field's value bytes are decoded leniently (DecodeLeniently: UTF-8, falling back to
    /// Latin-1/Windows-1252 per invalid byte) rather than strictly as UTF-8. This matters because some
    /// real-world exporters don't actually write valid UTF-8 despite ADIF nominally expecting it --
    /// confirmed against a real QRZ Logbook export containing a NAME field with a raw Windows-1252 byte
    /// (0xF1, the Windows-1252 code point for n-with-tilde) instead of a proper 2-byte UTF-8 sequence,
    /// with QRZ's own length tag computed against *that* (1-byte) encoding. Decoding strictly as UTF-8
    /// first (the .NET default, via
    /// StreamReader/File.ReadAllText) silently replaces the invalid byte with U+FFFD before this code
    /// ever runs -- and U+FFFD re-encodes to 3 bytes, not the original 1, which desyncs this field's own
    /// remaining byte count and truncates it. Byte-native lenient decoding avoids the whole problem: the
    /// exact byte range named by the length tag is decoded on its own terms, whatever mix of valid UTF-8
    /// and stray legacy-encoded bytes it contains.</summary>
    public static List<AdifRecord> ReadAll(byte[] bytes)
    {
        var records = new List<AdifRecord>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int i = 0;
        int len = bytes.Length;
        while (i < len)
        {
            if (bytes[i] != (byte)'<')
            {
                i++; // free text between tags (header preamble, whitespace) is ignored
                continue;
            }

            int close = Array.IndexOf(bytes, (byte)'>', i + 1);
            if (close < 0)
            {
                break; // malformed trailing tag — stop parsing
            }

            string tagContent = DecodeLeniently(bytes, i + 1, close - i - 1);
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

            if (parts.Length >= 2 && int.TryParse(parts[1], out int valueByteLength) && valueByteLength > 0)
            {
                int valueEnd = Math.Min(i + valueByteLength, len);

                // Defensive recovery: a length tag that doesn't actually land on the next tag/EOR/EOH is
                // simply wrong (confirmed against a real QRZ Logbook export: its own exporter undercounts
                // any field whose value already contains a multi-byte character, e.g. an embedded U+FFFD
                // from an earlier, unrelated corruption -- 3 UTF-8 bytes counted as 1). Rather than
                // silently truncating the value there, extend to the next literal '<' instead, which
                // ADIF's grammar guarantees marks a value's real end (a value can never itself contain an
                // unescaped '<' -- that's what starts the next tag). No effect on well-formed files, where
                // the declared length is already correct and this check always passes on the first try.
                int decodeEnd = valueEnd;
                if (!LooksLikeTagBoundary(bytes, valueEnd, len))
                {
                    int nextTagStart = Array.IndexOf(bytes, (byte)'<', i);
                    if (nextTagStart > i)
                    {
                        valueEnd = nextTagStart;
                        // The gap between the (wrong) declared length and the real tag boundary is
                        // formatting whitespace in the source file, not part of the value -- trim it
                        // from what gets stored, though the parser itself still advances past all of it.
                        decodeEnd = valueEnd;
                        while (decodeEnd > i && bytes[decodeEnd - 1] is (byte)'\r' or (byte)'\n' or (byte)' ' or (byte)'\t') decodeEnd--;
                    }
                }

                current[name] = DecodeLeniently(bytes, i, decodeEnd - i);
                i = valueEnd;
            }
            else
            {
                current[name] = string.Empty;
            }
        }

        return records;
    }

    /// <summary>True if position pos is the start of the next tag (allowing leading whitespace/newlines,
    /// common for readability between tags) or end of file. Used to sanity-check a value's declared
    /// length against where it actually lands -- see the recovery logic above.</summary>
    private static bool LooksLikeTagBoundary(byte[] bytes, int pos, int len)
    {
        int p = pos;
        while (p < len && bytes[p] is (byte)'\r' or (byte)'\n' or (byte)' ' or (byte)'\t') p++;
        return p >= len || bytes[p] == (byte)'<';
    }

    /// <summary>Decodes a byte range as UTF-8 where valid, falling back to treating any individual byte
    /// that isn't part of a valid UTF-8 sequence as a Latin-1/Windows-1252 code point (byte value ==
    /// Unicode code point) -- the standard "mojibake recovery" heuristic, and correct for the accented-
    /// Latin-letter range (0xA0-0xFF) that's the actual real-world case this exists for. This never
    /// throws and never substitutes U+FFFD, unlike Encoding.UTF8.GetString's default behavior.</summary>
    private static string DecodeLeniently(byte[] bytes, int start, int length)
    {
        var result = new StringBuilder(length);
        int end = start + length;
        int i = start;
        while (i < end)
        {
            byte b = bytes[i];
            if (b < 0x80)
            {
                result.Append((char)b);
                i++;
            }
            else if (TryDecodeUtf8Sequence(bytes, i, end, out int codePoint, out int sequenceLength))
            {
                if (codePoint > 0xFFFF)
                {
                    int adjusted = codePoint - 0x10000;
                    result.Append((char)(0xD800 + (adjusted >> 10)));
                    result.Append((char)(0xDC00 + (adjusted & 0x3FF)));
                }
                else
                {
                    result.Append((char)codePoint);
                }
                i += sequenceLength;
            }
            else
            {
                result.Append((char)b); // Latin-1/Windows-1252 single-byte fallback
                i++;
            }
        }
        return result.ToString();
    }

    /// <summary>Attempts to decode a well-formed multi-byte UTF-8 sequence starting at position pos
    /// (never a single ASCII byte -- callers only reach here for b &gt;= 0x80). Rejects anything that
    /// isn't a complete, properly-continued, non-overlong sequence within a valid code point range, so
    /// that lookalike-but-invalid byte patterns fall back to the Latin-1 path instead of decoding
    /// wrongly.</summary>
    private static bool TryDecodeUtf8Sequence(byte[] bytes, int pos, int end, out int codePoint, out int length)
    {
        codePoint = 0;
        length = 0;
        byte leadByte = bytes[pos];

        int expectedLength;
        int value;
        if ((leadByte & 0b1110_0000) == 0b1100_0000) { expectedLength = 2; value = leadByte & 0b0001_1111; }
        else if ((leadByte & 0b1111_0000) == 0b1110_0000) { expectedLength = 3; value = leadByte & 0b0000_1111; }
        else if ((leadByte & 0b1111_1000) == 0b1111_0000) { expectedLength = 4; value = leadByte & 0b0000_0111; }
        else return false;

        if (pos + expectedLength > end) return false;

        for (int k = 1; k < expectedLength; k++)
        {
            byte continuationByte = bytes[pos + k];
            if ((continuationByte & 0b1100_0000) != 0b1000_0000) return false;
            value = (value << 6) | (continuationByte & 0b0011_1111);
        }

        // Reject overlong encodings and out-of-range code points -- a technically-well-formed-looking
        // sequence that's actually overlong should fall back to Latin-1 rather than decode to the wrong
        // (too-small) code point.
        bool overlong = expectedLength switch
        {
            2 => value < 0x80,
            3 => value < 0x800,
            4 => value < 0x10000,
            _ => true,
        };
        if (overlong || value > 0x10FFFF) return false;

        codePoint = value;
        length = expectedLength;
        return true;
    }
}
