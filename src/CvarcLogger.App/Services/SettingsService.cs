using System.IO;
using System.Text.Json;
using CvarcLogger.Core.Rig;

namespace CvarcLogger.App.Services;

public enum LookupServicePreference
{
    Callook,
    Qrz,
    QrzCq
}

/// <summary>Simple JSON-file-backed app preferences (not QSO data, not credentials).</summary>
public class SettingsService
{
    private readonly string _filePath;
    private readonly AppSettingsData _data;

    public SettingsService()
    {
        _filePath = Path.Combine(App.DataDirectory, "settings.json");
        _data = Load();
        if (MigrateRadioProfiles(_data)) Save();
    }

    public LookupServicePreference PreferredLookupService
    {
        get => _data.PreferredLookupService;
        set { _data.PreferredLookupService = value; Save(); }
    }

    public int? LastUsedStationProfileId
    {
        get => _data.LastUsedStationProfileId;
        set { _data.LastUsedStationProfileId = value; Save(); }
    }

    public bool CatEnabled
    {
        get => _data.CatEnabled;
        set { _data.CatEnabled = value; Save(); }
    }

    public bool LaunchRigctldAutomatically
    {
        get => _data.LaunchRigctldAutomatically;
        set { _data.LaunchRigctldAutomatically = value; Save(); }
    }

    /// <summary>The stored path if it still points at a real file (respects an explicit user
    /// customization, or a bundled path that's still valid); otherwise re-resolves against the
    /// *currently running* exe's own folder. This makes the setting self-healing across copying the
    /// exe to a new location or switching between the dev build and a published one, rather than
    /// permanently freezing whatever location resolved on the very first run.</summary>
    public string RigctldExecutablePath
    {
        get
        {
            string stored = _data.RigctldExecutablePath;
            if (!string.IsNullOrWhiteSpace(stored) && File.Exists(stored)) return stored;

            string bundled = Path.Combine(AppContext.BaseDirectory, "hamlib", "rigctld.exe");
            return File.Exists(bundled) ? bundled : stored;
        }
        set { _data.RigctldExecutablePath = value; Save(); }
    }

    public int RigctldTcpPort
    {
        get => _data.RigctldTcpPort;
        set { _data.RigctldTcpPort = value; Save(); }
    }

    public int ActiveRadioIndex
    {
        get => _data.ActiveRadioIndex;
        set { _data.ActiveRadioIndex = value; Save(); }
    }

    /// <summary>Mutate entries in place, then call SaveRadioProfiles().</summary>
    public List<RadioProfile> RadioProfiles => _data.RadioProfiles;

    public void SaveRadioProfiles() => Save();

    /// <summary>Keys of QSO log grid columns the user has hidden via the "Columns..." picker (Callsign
    /// and Date/Time UTC are always shown and never appear here). Mutate in place, then call
    /// SaveHiddenLogColumns().</summary>
    public HashSet<string> HiddenLogColumns => _data.HiddenLogColumns;

    public void SaveHiddenLogColumns() => Save();

    /// <summary>Seeds a column's HiddenLogColumns membership from <paramref name="defaultVisible"/> the
    /// first time this key is ever seen for this settings file, then never touches it again — so a
    /// column added in a later app version starts hidden (or visible) as the code intends, without
    /// silently overriding a choice the user already made by toggling it in the picker. A no-op on
    /// every call after the first for a given key.</summary>
    public void EnsureLogColumnDefault(string key, bool defaultVisible)
    {
        if (!_data.SeenLogColumnKeys.Add(key)) return;
        if (!defaultVisible) _data.HiddenLogColumns.Add(key);
        Save();
    }

    /// <summary>Full path to the active QSO database, or null to use the default
    /// (%LOCALAPPDATA%\CvarcLogger\cvarclogger.db). Switching this takes effect on next launch — the
    /// DbContext's connection string is fixed for the process's lifetime.</summary>
    public string? CurrentDatabasePath
    {
        get => _data.CurrentDatabasePath;
        set { _data.CurrentDatabasePath = value; Save(); }
    }

    /// <summary>Resolves the effective database path without needing a full SettingsService instance
    /// (used at startup, before the DI container that would normally provide one exists). Reads
    /// settings.json directly; falls back to the default path on any error, same as normal Load().</summary>
    public static string ResolveActiveDatabasePath()
    {
        string settingsPath = Path.Combine(App.DataDirectory, "settings.json");
        string defaultDbPath = Path.Combine(App.DataDirectory, "cvarclogger.db");

        if (!File.Exists(settingsPath)) return defaultDbPath;
        try
        {
            var data = JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(settingsPath));
            return !string.IsNullOrWhiteSpace(data?.CurrentDatabasePath) ? data.CurrentDatabasePath : defaultDbPath;
        }
        catch
        {
            return defaultDbPath;
        }
    }

    private AppSettingsData Load()
    {
        if (!File.Exists(_filePath)) return new AppSettingsData();
        try
        {
            return JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(_filePath)) ?? new AppSettingsData();
        }
        catch
        {
            return new AppSettingsData();
        }
    }

    /// <summary>Appends any newly-introduced default radio profiles to a settings file saved by an
    /// older version of the app, so new radios show up without the user having to delete settings.json.
    /// Returns true if anything was added (so the caller knows to persist it).</summary>
    private static bool MigrateRadioProfiles(AppSettingsData data)
    {
        var existingNames = data.RadioProfiles.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool added = false;
        foreach (var defaultProfile in new AppSettingsData().RadioProfiles)
        {
            if (existingNames.Add(defaultProfile.Name))
            {
                data.RadioProfiles.Add(defaultProfile);
                added = true;
            }
        }
        return added;
    }

    private void Save()
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_data));
    }

    private class AppSettingsData
    {
        public LookupServicePreference PreferredLookupService { get; set; } = LookupServicePreference.Callook;
        public int? LastUsedStationProfileId { get; set; }
        public string? CurrentDatabasePath { get; set; }
        public HashSet<string> HiddenLogColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SeenLogColumnKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool CatEnabled { get; set; }
        public bool LaunchRigctldAutomatically { get; set; } = true;
        public string RigctldExecutablePath { get; set; } = ResolveDefaultRigctldPath();
        public int RigctldTcpPort { get; set; } = 4532;
        public int ActiveRadioIndex { get; set; }

        public List<RadioProfile> RadioProfiles { get; set; } = new()
        {
            // Model IDs confirmed via `rigctld --list` against Hamlib 4.6.3.
            new RadioProfile { Name = "Elecraft K4D", HamlibModelId = 2047, ComPort = "COM3", BaudRate = 38400 },
            new RadioProfile { Name = "Yaesu FT-991A", HamlibModelId = 1035, ComPort = "COM4", BaudRate = 38400 },
            new RadioProfile { Name = "Kenwood TS-890", HamlibModelId = 2041, ComPort = "COM5", BaudRate = 115200 },
            new RadioProfile { Name = "Yaesu FT-920", HamlibModelId = 1014, ComPort = "COM7", BaudRate = 4800 },
        };

        /// <summary>Prefers the copy of rigctld.exe bundled alongside the app (vendor/hamlib/, copied
        /// into a "hamlib" subfolder next to the exe at build/publish time) so CAT control works out
        /// of the box on a freshly copied install. Falls back to bare "rigctld.exe" (PATH lookup) if
        /// the bundled copy isn't present, e.g. when running from source without a publish step.</summary>
        private static string ResolveDefaultRigctldPath()
        {
            string bundled = Path.Combine(AppContext.BaseDirectory, "hamlib", "rigctld.exe");
            return File.Exists(bundled) ? bundled : "rigctld.exe";
        }
    }
}
