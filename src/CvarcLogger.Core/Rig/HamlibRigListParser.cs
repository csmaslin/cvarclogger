namespace CvarcLogger.Core.Rig;

/// <summary>Parses the table `rigctld --list` prints to stdout. Columns are fixed-width, and Mfg/Model
/// values can themselves contain spaces (e.g. "DTTS Microwave Society", "NET rigctl"), so this locates
/// each column's start offset from the header row itself rather than splitting on whitespace or
/// hardcoding byte offsets — it keeps working even if a future Hamlib build widens a column.</summary>
public static class HamlibRigListParser
{
    public static IReadOnlyList<HamlibRigInfo> Parse(string stdout)
    {
        string[] lines = stdout.Replace("\r\n", "\n").Split('\n');

        int headerIndex = Array.FindIndex(lines, l =>
            l.Contains("Mfg", StringComparison.Ordinal) &&
            l.Contains("Model", StringComparison.Ordinal) &&
            l.Contains("Status", StringComparison.Ordinal));
        if (headerIndex < 0) return Array.Empty<HamlibRigInfo>();

        string header = lines[headerIndex];
        int mfgStart = header.IndexOf("Mfg", StringComparison.Ordinal);
        int modelStart = header.IndexOf("Model", StringComparison.Ordinal);
        int versionStart = header.IndexOf("Version", StringComparison.Ordinal);
        int statusStart = header.IndexOf("Status", StringComparison.Ordinal);
        int macroStart = header.IndexOf("Macro", StringComparison.Ordinal);
        if (mfgStart < 0 || modelStart < 0 || versionStart < 0 || statusStart < 0)
            return Array.Empty<HamlibRigInfo>();

        var rigs = new List<HamlibRigInfo>();
        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length <= statusStart) continue;

            string idPart = line[..Math.Min(mfgStart, line.Length)].Trim();
            if (!int.TryParse(idPart, out int id) || id <= 0) continue;

            string mfg = line[mfgStart..Math.Min(modelStart, line.Length)].Trim();
            string model = line[modelStart..Math.Min(versionStart, line.Length)].Trim();
            string status = (macroStart > statusStart && macroStart <= line.Length
                ? line[statusStart..macroStart]
                : line[statusStart..]).Trim();

            rigs.Add(new HamlibRigInfo(id, mfg, model, status));
        }

        return rigs;
    }
}
