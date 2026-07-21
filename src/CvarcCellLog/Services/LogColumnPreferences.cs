using CvarcCellLog.Models;

namespace CvarcCellLog.Services;

/// <summary>Persists which QSO Log columns are visible and in what order -- a comma-separated list of
/// LogColumnKey names, Preferences-backed like the app's other single-value settings.</summary>
public static class LogColumnPreferences
{
    private const string Key = "QsoLogColumns";

    public static List<LogColumnKey> Load()
    {
        string? stored = Preferences.Default.Get(Key, (string?)null);
        if (string.IsNullOrWhiteSpace(stored)) return LogColumns.DefaultOrder.ToList();

        var keys = stored.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.TryParse<LogColumnKey>(s, out var key) ? (LogColumnKey?)key : null)
            .Where(key => key.HasValue)
            .Select(key => key!.Value)
            .ToList();

        return keys.Count > 0 ? keys : LogColumns.DefaultOrder.ToList();
    }

    public static void Save(IEnumerable<LogColumnKey> keys) =>
        Preferences.Default.Set(Key, string.Join(",", keys));
}
