namespace CvarcCellLog.Services;

/// <summary>Resolves and persists which database file is active, ported from the WPF app's
/// SettingsService.ResolveActiveDatabasePath/CurrentDatabasePath -- Preferences-backed here instead of a
/// settings.json file, since MAUI already uses Preferences for other single-value settings in this app
/// (LastUsedStationProfileId, PreferredLookupService). Static (not DI) because MauiProgram needs the
/// resolved path before the DI container that would host it exists, same reason the WPF version is static.</summary>
public static class DatabasePathService
{
    private const string CurrentDatabasePathKey = "CurrentDatabasePath";

    public static string DefaultPath => Path.Combine(FileSystem.AppDataDirectory, "cvarclogger.db");

    /// <summary>Self-healing: if the stored path's parent directory no longer exists (removable storage
    /// unmounted, the document provider that backed it uninstalled, etc.), forgets it and falls back to
    /// the default path -- same rule as the WPF app's ResolveActiveDatabasePath.</summary>
    public static string ResolveActivePath()
    {
        string? stored = Preferences.Default.Get(CurrentDatabasePathKey, (string?)null);
        if (stored is not null)
        {
            string? dir = Path.GetDirectoryName(stored);
            if (dir is not null && Directory.Exists(dir)) return stored;
            Preferences.Default.Remove(CurrentDatabasePathKey);
        }
        return DefaultPath;
    }

    public static void SetActivePath(string path) => Preferences.Default.Set(CurrentDatabasePathKey, path);
}
