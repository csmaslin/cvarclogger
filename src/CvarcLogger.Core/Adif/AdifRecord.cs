namespace CvarcLogger.Core.Adif;

/// <summary>A single ADIF record (header fields or one QSO's fields), keyed by ADIF field name, case-insensitive.</summary>
public class AdifRecord
{
    private readonly Dictionary<string, string> _fields;

    public AdifRecord() => _fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public AdifRecord(IDictionary<string, string> fields) =>
        _fields = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Fields => _fields;

    public string? Get(string name) => _fields.TryGetValue(name, out var v) ? v : null;

    public void Set(string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _fields.Remove(name);
            return;
        }
        _fields[name] = value;
    }

    public bool Contains(string name) => _fields.ContainsKey(name);
}
