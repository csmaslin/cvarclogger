using System.IO;
using System.Text.Json;
using CvarcLogger.Core.Rig;

namespace CvarcLogger.App.Services;

/// <summary>Which CAT backend the entry form's Connect CAT uses. Mutually exclusive by construction —
/// stored as the two underlying CatEnabled/InternetRadioEnabled bools (which RigControlCoordinator and
/// QsoEntryViewModel read directly), but surfaced as a single choice so the UI can't leave both on.</summary>
public enum CatSource
{
    Off,
    Usb,
    Internet
}

/// <summary>WSJT-X data reception mode: GridTracker2 relay or direct multicast.</summary>
public enum WsjtxMode
{
    GridTracker2Relay,
    Multicast
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

    public int? LastUsedStationProfileId
    {
        get => _data.LastUsedStationProfileId;
        set { _data.LastUsedStationProfileId = value; Save(); }
    }

    public string ProgramOwnership
    {
        get => _data.ProgramOwnership;
        set { _data.ProgramOwnership = value; Save(); }
    }

    public bool CatEnabled
    {
        get => _data.CatEnabled;
        set { _data.CatEnabled = value; Save(); }
    }

    /// <summary>The single mutually-exclusive CAT source selector (see CatSource). Reading prefers
    /// Internet over Usb if both underlying flags were somehow set; writing forces exactly one (or
    /// neither) of CatEnabled/InternetRadioEnabled on, so the two can never both be true again.</summary>
    public CatSource CatSource
    {
        get => _data.InternetRadioEnabled ? CatSource.Internet
             : _data.CatEnabled ? CatSource.Usb
             : CatSource.Off;
        set
        {
            _data.CatEnabled = value == CatSource.Usb;
            _data.InternetRadioEnabled = value == CatSource.Internet;
            Save();
        }
    }

    public bool LaunchRigctldAutomatically
    {
        get => _data.LaunchRigctldAutomatically;
        set { _data.LaunchRigctldAutomatically = value; Save(); }
    }

    /// <summary>When enabled, each logged/edited QSO is broadcast as a single ADIF record over UDP so
    /// companion apps like GridTracker2 can plot it on their map/grid-tracking in real time.</summary>
    public bool GridTrackerEnabled
    {
        get => _data.GridTrackerEnabled;
        set { _data.GridTrackerEnabled = value; Save(); }
    }

    public string GridTrackerHost
    {
        get => _data.GridTrackerHost;
        set { _data.GridTrackerHost = value; Save(); }
    }

    public int GridTrackerPort
    {
        get => _data.GridTrackerPort;
        set { _data.GridTrackerPort = value; Save(); }
    }

    /// <summary>When enabled, listens on port 2238 for WSJT-X's logged-QSO data as relayed by
    /// GridTracker2's own "Forward UDP messages" feature (see WsjtxUdpListenerService for the full
    /// topology and port-conflict history) and adds each one to the log automatically. Deliberately not
    /// forwarded back to GridTracker2 -- see WsjtxUdpListenerService.</summary>
    public bool WsjtxEnabled
    {
        get => _data.WsjtxEnabled;
        set { _data.WsjtxEnabled = value; Save(); }
    }

    /// <summary>UDP port for receiving WSJT-X logged QSOs relayed by GridTracker2 or direct multicast.</summary>
    public int WsjtxPort
    {
        get => _data.WsjtxPort;
        set { _data.WsjtxPort = value; Save(); }
    }

    /// <summary>WSJT-X reception mode: GridTracker2 relay or direct multicast.</summary>
    public WsjtxMode WsjtxMode
    {
        get => _data.WsjtxMode;
        set { _data.WsjtxMode = value; Save(); }
    }

    /// <summary>Convenience property: true = multicast mode, false = GridTracker2 relay.</summary>
    public bool WsjtxUseMulticast
    {
        get => _data.WsjtxMode == WsjtxMode.Multicast;
        set { _data.WsjtxMode = value ? WsjtxMode.Multicast : WsjtxMode.GridTracker2Relay; Save(); }
    }

    /// <summary>Multicast address for WSJT-X direct reception (default 224.0.0.1).</summary>
    public string WsjtxMulticastAddress
    {
        get => _data.WsjtxMulticastAddress;
        set { _data.WsjtxMulticastAddress = value; Save(); }
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

    /// <summary>When enabled, the entry form's Connect CAT / auto-fill uses the network K4 (Internet
    /// Control) instead of the Hamlib/rigctld serial path. Takes precedence over CatEnabled when both are
    /// on.</summary>
    public bool InternetRadioEnabled
    {
        get => _data.InternetRadioEnabled;
        set { _data.InternetRadioEnabled = value; Save(); }
    }

    /// <summary>Host/IP of an Elecraft K4 reachable over the network for "Internet Control (CAT)" (its
    /// native TCP command protocol, distinct from the Hamlib/rigctld serial radios above). The optional
    /// password for it isn't stored here -- it goes to the encrypted ICredentialStore instead.</summary>
    public string InternetRadioHost
    {
        get => _data.InternetRadioHost;
        set { _data.InternetRadioHost = value; Save(); }
    }

    /// <summary>TCP port for the network K4 (Elecraft's default is 9200).</summary>
    public int InternetRadioPort
    {
        get => _data.InternetRadioPort;
        set { _data.InternetRadioPort = value; Save(); }
    }

    /// <summary>Mutate entries in place, then call SaveRadioProfiles().</summary>
    public List<RadioProfile> RadioProfiles => _data.RadioProfiles;

    public void SaveRadioProfiles() => Save();

    /// <summary>Legacy global hidden-columns set, from before column/field visibility became per-mode
    /// (see GetHiddenColumns). No longer read directly by the grid or entry form -- kept for two things
    /// only: EnsureLogColumnDefault below seeds new column keys into it, and GetHiddenColumns seeds a
    /// mode's set from a copy of it the first time that mode is touched, so existing hidden-column
    /// choices carry forward instead of resetting when this feature first runs. Callsign and Date/Time
    /// UTC are always shown and never appear here.</summary>
    public HashSet<string> HiddenLogColumns => _data.HiddenLogColumns;

    public void SaveHiddenLogColumns() => Save();

    /// <summary>Saved display order for QSO log grid columns, keyed by column key with values being the
    /// desired 0-based DisplayIndex. A column missing from this map (never reordered, or added in a
    /// later app version) keeps its XAML-declared relative order and is placed after every column that
    /// does have a saved position.</summary>
    public IReadOnlyDictionary<string, int> LogColumnOrder => _data.LogColumnOrder;

    public void SaveLogColumnOrder(IReadOnlyDictionary<string, int> order)
    {
        _data.LogColumnOrder = new Dictionary<string, int>(order, StringComparer.OrdinalIgnoreCase);
        Save();
    }

    /// <summary>Saved pixel width for QSO log grid columns, keyed by column key. A column missing from
    /// this map (never resized, or added in a later app version) keeps its XAML-declared default
    /// width.</summary>
    public IReadOnlyDictionary<string, double> LogColumnWidths => _data.LogColumnWidths;

    public void SaveLogColumnWidths(IReadOnlyDictionary<string, double> widths)
    {
        _data.LogColumnWidths = new Dictionary<string, double>(widths, StringComparer.OrdinalIgnoreCase);
        Save();
    }

    /// <summary>The entry form's share of the vertical space it splits with the log grid, as a fraction
    /// between 0 and 1 (0.5 = an even split, the default for a fresh install). Stored as a ratio rather
    /// than a pixel height so the saved layout still makes sense when the app reopens on a different
    /// window size or monitor -- a pixel height saved on a maximized window would swallow the whole
    /// grid on a smaller one. Null until the operator first drags the splitter.</summary>
    public double? EntryFormSplitRatio => _data.EntryFormSplitRatio;

    /// <summary>Persists the splitter position. Values are clamped to leave both panes usable, so a
    /// drag that pins the splitter to one extreme can't save a state the operator can't drag back out
    /// of on the next launch.</summary>
    public void SaveEntryFormSplitRatio(double ratio)
    {
        _data.EntryFormSplitRatio = Math.Clamp(ratio, 0.15, 0.85);
        Save();
    }

    /// <summary>Keys of columns/fields hidden for one specific Log Entry Mode (Normal/Contest/Sota/Pota/
    /// Net/All each get their own independent set, keyed by QsoEntryMode.ToString()) -- shared by both
    /// the QSO log grid and the entry form, so switching mode changes what's visible in both places
    /// together (see QsoLogViewModel.IsColumnVisible / QsoEntryViewModel.IsFieldVisible). A mode's set is
    /// seeded from a copy of the legacy global HiddenLogColumns the first time that mode is ever touched,
    /// so existing column choices carry forward identically before anyone customizes a specific mode;
    /// after that the two are independent. Mutate in place, then call SaveHiddenColumns().</summary>
    public HashSet<string> GetHiddenColumns(string mode)
    {
        if (!_data.HiddenColumnsByMode.TryGetValue(mode, out var set))
        {
            set = new HashSet<string>(_data.HiddenLogColumns, StringComparer.OrdinalIgnoreCase);
            _data.HiddenColumnsByMode[mode] = set;
        }
        return set;
    }

    public void SaveHiddenColumns() => Save();

    /// <summary>User-customized display label for one Log Entry Mode's tab in the Column Visibility
    /// picker (e.g. renaming "Contest" to "Field Day"); cosmetic only, doesn't affect the sidebar mode
    /// buttons or anything else. Falls back to <paramref name="defaultLabel"/> when never renamed.</summary>
    public string GetModeTabLabel(string mode, string defaultLabel) =>
        _data.ModeTabLabels.TryGetValue(mode, out var label) ? label : defaultLabel;

    public void SetModeTabLabel(string mode, string label)
    {
        _data.ModeTabLabels[mode] = label;
        Save();
    }

    /// <summary>Whether the entry form's fields are locked against drag-and-drop repositioning for one
    /// specific Log Entry Mode -- independent per mode, so e.g. a carefully-arranged SOTA layout can be
    /// locked down while a Custom tab someone's still experimenting with stays freely draggable. Default
    /// false (unlocked), matching drag-and-drop's original always-on behavior.</summary>
    public bool GetModeFieldsLocked(string mode) =>
        _data.ModeFieldsLocked.TryGetValue(mode, out var locked) && locked;

    public void SetModeFieldsLocked(string mode, bool locked)
    {
        _data.ModeFieldsLocked[mode] = locked;
        Save();
    }

    /// <summary>Saved (Row, Position) for each entry-form field, independently per Log Entry Mode --
    /// what the click-and-drag layout editor writes to. A field missing from a mode's map (never
    /// dragged, or added in a later app version) falls back to its XAML-declared default row/position.
    /// Auto-creates an empty map for a mode never touched before. Mutate in place, then call
    /// SaveEntryFormFieldPositions().</summary>
    public Dictionary<string, EntryFormFieldPosition> GetEntryFormFieldPositions(string mode)
    {
        if (!_data.EntryFormFieldPositionsByMode.TryGetValue(mode, out var map))
        {
            map = new Dictionary<string, EntryFormFieldPosition>(StringComparer.OrdinalIgnoreCase);
            _data.EntryFormFieldPositionsByMode[mode] = map;
        }
        return map;
    }

    public void SaveEntryFormFieldPositions() => Save();

    /// <summary>Whether an entry-form field the operator can toggle sticky ("static") on/off keeps its
    /// value across QSOs instead of clearing in ResetForNextQso. Global, not per-mode -- it's a workflow
    /// habit rather than a display choice. Stores the exceptions only (fields explicitly turned OFF), so
    /// every such field defaults to static/on with no migration needed, matching the hardcoded behavior
    /// that existed before this became configurable.</summary>
    public bool IsFieldStatic(string key) => !_data.NonStaticFields.Contains(key);

    public void SetFieldStatic(string key, bool isStatic)
    {
        if (isStatic) _data.NonStaticFields.Remove(key);
        else _data.NonStaticFields.Add(key);
        Save();
    }

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

    /// <summary>Full path to the active QSO database, or null to use the default (see
    /// DefaultDatabasePath). Switching this takes effect on next launch — the DbContext's connection
    /// string is fixed for the process's lifetime.</summary>
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

        if (File.Exists(settingsPath))
        {
            try
            {
                var data = JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(settingsPath));
                if (data is not null && !string.IsNullOrWhiteSpace(data.CurrentDatabasePath))
                {
                    string? dir = Path.GetDirectoryName(data.CurrentDatabasePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return data.CurrentDatabasePath;

                    // The folder that used to hold this log is gone -- most often an old app version's
                    // install/publish folder that was since replaced or deleted, or removable media
                    // that's no longer attached. Trusting this path would make SQLite fail to even
                    // create the file (the parent directory doesn't exist), crashing the app before any
                    // window can show. Clear it so future launches fall back to the default path instead
                    // of repeating this failure forever.
                    data.CurrentDatabasePath = null;
                    try { File.WriteAllText(settingsPath, JsonSerializer.Serialize(data)); } catch { /* best-effort */ }
                }
            }
            catch
            {
                // Fall through to the default below, same as a missing settings file.
            }
        }

        return DefaultDatabasePath();
    }

    /// <summary>Where a brand-new database is created when nothing else has been chosen: alongside
    /// everything else CvarcLogger creates -- see App.DataDirectory for the legacy-preserving logic that
    /// decides whether that's next to the exe or an existing %LOCALAPPDATA% install.</summary>
    private static string DefaultDatabasePath() => Path.Combine(App.DataDirectory, "cvarclogger.db");

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
        public int? LastUsedStationProfileId { get; set; }
        public string ProgramOwnership { get; set; } = "Charles.S.Maslin";
        public string? CurrentDatabasePath { get; set; }
        public HashSet<string> HiddenLogColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> SeenLogColumnKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> LogColumnOrder { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> LogColumnWidths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public double? EntryFormSplitRatio { get; set; }

        // Keyed by QsoEntryMode.ToString() ("Normal", "Contest", "Sota", "Pota", "Net", "All") so each
        // Log Entry Mode's column/field visibility and entry-form layout are each fully independent --
        // see SettingsService.GetHiddenColumns/GetEntryFormFieldPositions.
        public Dictionary<string, HashSet<string>> HiddenColumnsByMode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, EntryFormFieldPosition>> EntryFormFieldPositionsByMode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ModeTabLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> ModeFieldsLocked { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> NonStaticFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool CatEnabled { get; set; }
        public bool LaunchRigctldAutomatically { get; set; }

        public bool GridTrackerEnabled { get; set; }
        public string GridTrackerHost { get; set; } = "127.0.0.1";
        public int GridTrackerPort { get; set; } = 2240;
        public bool WsjtxEnabled { get; set; }
        public int WsjtxPort { get; set; } = 2238;
        public WsjtxMode WsjtxMode { get; set; } = WsjtxMode.GridTracker2Relay;
        public string WsjtxMulticastAddress { get; set; } = "224.0.0.1";
        public string RigctldExecutablePath { get; set; } = ResolveDefaultRigctldPath();
        public int RigctldTcpPort { get; set; } = 4532;
        public int ActiveRadioIndex { get; set; } = -1;

        public bool InternetRadioEnabled { get; set; }
        public string InternetRadioHost { get; set; } = string.Empty;
        public int InternetRadioPort { get; set; } = 9200;

        public List<RadioProfile> RadioProfiles { get; set; } = new()
        {
            new RadioProfile { Name = "Radio 1" },
            new RadioProfile { Name = "Radio 2" },
            new RadioProfile { Name = "Radio 3" },
            new RadioProfile { Name = "Radio 4" },
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

        /// <summary>Checks if Hamlib (rigctld.exe) is available either bundled with the app or in system PATH.</summary>
        public static bool IsHamlibAvailable()
        {
            string bundled = Path.Combine(AppContext.BaseDirectory, "hamlib", "rigctld.exe");
            if (File.Exists(bundled)) return true;

            string[] commonPaths = new[]
            {
                @"C:\Program Files\hamlib\bin\rigctld.exe",
                @"C:\Program Files (x86)\hamlib\bin\rigctld.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "hamlib", "bin", "rigctld.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "hamlib", "bin", "rigctld.exe")
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path)) return true;
            }

            return false;
        }
    }
}
