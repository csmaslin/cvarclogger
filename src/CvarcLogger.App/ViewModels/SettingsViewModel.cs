using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Lookup;
using CvarcLogger.Core.Rig;

namespace CvarcLogger.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ICredentialStore _credentialStore;
    private readonly DialogService _dialogService;
    private readonly RigControlCoordinator _rigCoordinator;
    private readonly HamlibRigCatalog _rigCatalog;
    private readonly GridTrackerBroadcastService _gridTrackerBroadcast;
    private readonly WsjtxUdpListenerService _wsjtxListener;
    private readonly QrzLookupService _qrzLookupService;
    private readonly QrzCqLookupService _qrzCqLookupService;

    /// <summary>Well-known, always-registered callsign (ARRL HQ) used purely to exercise the login +
    /// lookup round-trip when the user clicks Test -- there's no meaningful "ping" endpoint on either
    /// API, so a real lookup is the only way to confirm the credentials actually work.</summary>
    private const string TestLookupCallsign = "W1AW";

    [ObservableProperty] private string qrzUsername = string.Empty;
    [ObservableProperty] private string qrzPassword = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QrzStatusText))]
    private bool isQrzConfigured;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QrzStatusText))]
    private bool qrzTestedGood;
    [ObservableProperty] private string qrzCqUsername = string.Empty;
    [ObservableProperty] private string qrzCqPassword = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QrzCqStatusText))]
    private bool isQrzCqConfigured;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QrzCqStatusText))]
    private bool qrzCqTestedGood;

    /// <summary>"Confirmed" only after a successful Test click this session (a real login+lookup
    /// round-trip); "Configured" just means credentials are saved, not verified; saving new credentials
    /// clears a prior test result since it hasn't been re-verified.</summary>
    public string QrzStatusText => QrzTestedGood ? "Confirmed" : IsQrzConfigured ? "Configured" : "Not Configured";
    public string QrzCqStatusText => QrzCqTestedGood ? "Confirmed" : IsQrzCqConfigured ? "Configured" : "Not Configured";

    [ObservableProperty] private CatSource catSource;
    [ObservableProperty] private bool launchRigctldAutomatically;
    [ObservableProperty] private string rigctldExecutablePath = "rigctld.exe";
    [ObservableProperty] private string rigctldTcpPort = "4532";
    [ObservableProperty] private int activeRadioIndex;

    [ObservableProperty] private bool gridTrackerEnabled;
    [ObservableProperty] private string gridTrackerHost = "127.0.0.1";
    [ObservableProperty] private string gridTrackerPort = "2240";

    [ObservableProperty] private bool wsjtxEnabled;
    [ObservableProperty] private string wsjtxPort = "2238";
    [ObservableProperty] private bool wsjtxUseMulticast;
    [ObservableProperty] private string wsjtxMulticastAddress = "224.0.0.1";

    // Internet Control (CAT): a network-reachable Elecraft K4 (its native TCP protocol, default port
    // 9200), distinct from the Hamlib/rigctld serial radios above. Password is optional (the K4 protocol
    // itself needs no auth) and, when set, stored encrypted via ICredentialStore, never in settings.json.
    private const string InternetRadioCredentialKey = "INTERNET_RADIO";
    [ObservableProperty] private string internetRadioHost = string.Empty;
    [ObservableProperty] private string internetRadioPortText = "9200";
    [ObservableProperty] private string internetRadioPassword = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InternetRadioStatusText))]
    private bool isInternetRadioConfigured;

    public string InternetRadioStatusText => IsInternetRadioConfigured ? "Configured" : "Not Configured";

    public ObservableCollection<RadioProfileEditorViewModel> RadioProfileEditors { get; } = new();
    public ObservableCollection<HamlibRigInfo> AvailableRigs { get; } = new();
    public ObservableCollection<string> AvailableComPorts { get; } = new();

    public SettingsViewModel(
        SettingsService settings,
        ICredentialStore credentialStore,
        DialogService dialogService,
        RigControlCoordinator rigCoordinator,
        HamlibRigCatalog rigCatalog,
        GridTrackerBroadcastService gridTrackerBroadcast,
        WsjtxUdpListenerService wsjtxListener,
        QrzLookupService qrzLookupService,
        QrzCqLookupService qrzCqLookupService)
    {
        _settings = settings;
        _credentialStore = credentialStore;
        _dialogService = dialogService;
        _rigCoordinator = rigCoordinator;
        _rigCatalog = rigCatalog;
        _gridTrackerBroadcast = gridTrackerBroadcast;
        _wsjtxListener = wsjtxListener;
        _qrzLookupService = qrzLookupService;
        _qrzCqLookupService = qrzCqLookupService;

        catSource = _settings.CatSource;
        launchRigctldAutomatically = _settings.LaunchRigctldAutomatically;
        rigctldExecutablePath = _settings.RigctldExecutablePath;
        rigctldTcpPort = _settings.RigctldTcpPort.ToString();
        activeRadioIndex = _settings.ActiveRadioIndex;

        gridTrackerEnabled = _settings.GridTrackerEnabled;
        gridTrackerHost = _settings.GridTrackerHost;
        gridTrackerPort = _settings.GridTrackerPort.ToString();

        wsjtxEnabled = _settings.WsjtxEnabled;
        wsjtxPort = _settings.WsjtxPort.ToString();
        wsjtxUseMulticast = _settings.WsjtxUseMulticast;
        wsjtxMulticastAddress = _settings.WsjtxMulticastAddress;

        internetRadioHost = _settings.InternetRadioHost;
        internetRadioPortText = _settings.InternetRadioPort.ToString();

        for (int i = 0; i < _settings.RadioProfiles.Count; i++)
        {
            var profile = _settings.RadioProfiles[i];
            string slotLabel = $"Radio {i + 1}";
            RadioProfileEditors.Add(new RadioProfileEditorViewModel(profile, slotLabel, AvailableRigs, AvailableComPorts));
        }
    }

    public async Task InitializeAsync()
    {
        var creds = await _credentialStore.LoadAsync(QrzLookupService.CredentialKey);
        IsQrzConfigured = creds is not null;
        if (creds is not null)
        {
            QrzUsername = creds.Value.Username;
            // DPAPI encryption is reversible by design (for this Windows user, on this machine) --
            // loading the saved password back so "Show password" can actually reveal what's stored,
            // rather than only ever showing freshly-typed text. Stays masked in the PasswordBox unless
            // the operator explicitly checks Show password, same as freshly-typed text would.
            QrzPassword = creds.Value.Password;
        }

        var qrzCqCreds = await _credentialStore.LoadAsync(QrzCqLookupService.CredentialKey);
        IsQrzCqConfigured = qrzCqCreds is not null;
        if (qrzCqCreds is not null)
        {
            QrzCqUsername = qrzCqCreds.Value.Username;
            QrzCqPassword = qrzCqCreds.Value.Password;
        }

        var internetCreds = await _credentialStore.LoadAsync(InternetRadioCredentialKey);
        IsInternetRadioConfigured = internetCreds is not null;
        // Load the saved password back so "Show password" can reveal what's stored (stays masked in the
        // PasswordBox otherwise), same reversible-DPAPI approach as the QRZ credentials above.
        if (internetCreds is not null) InternetRadioPassword = internetCreds.Value.Password;

        var rigs = await _rigCatalog.GetRigsAsync(_settings.RigctldExecutablePath);
        AvailableRigs.Clear();
        AvailableRigs.Add(HamlibRigInfo.None);
        foreach (var rig in rigs
                     .OrderBy(r => r.Manufacturer, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(r => r.Model, StringComparer.OrdinalIgnoreCase))
        {
            AvailableRigs.Add(rig);
        }

        foreach (var editor in RadioProfileEditors) editor.RefreshSelectedRig();

        RefreshAvailableComPorts();
    }

    /// <summary>Re-enumerates COM ports each time Settings opens, so a USB-serial adapter plugged in
    /// after the app started (or after Settings was last opened) shows up without a restart.</summary>
    [RelayCommand]
    private void RefreshAvailableComPorts()
    {
        AvailableComPorts.Clear();
        foreach (var port in SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            AvailableComPorts.Add(port);
        }
    }

    [RelayCommand]
    private async Task SaveQrzCredentialsAsync()
    {
        if (string.IsNullOrWhiteSpace(QrzUsername) || string.IsNullOrWhiteSpace(QrzPassword))
        {
            _dialogService.ShowError("Enter both a QRZ username and password.");
            return;
        }

        await _credentialStore.SaveAsync(QrzLookupService.CredentialKey, QrzUsername, QrzPassword);
        IsQrzConfigured = true;
        QrzTestedGood = false;
        _dialogService.ShowInfo("QRZ credentials saved.");
    }

    [RelayCommand]
    private async Task ClearQrzCredentialsAsync()
    {
        await _credentialStore.DeleteAsync(QrzLookupService.CredentialKey);
        QrzUsername = string.Empty;
        QrzPassword = string.Empty;
        IsQrzConfigured = false;
        QrzTestedGood = false;
    }

    /// <summary>Saves the entered credentials (same as Save) then performs a real lookup against
    /// TestLookupCallsign, so "Test" actually proves the username/password work rather than just
    /// checking they're non-empty. Sets QrzTestedGood so the status line reflects a verified
    /// connection, not just "credentials are present".</summary>
    [RelayCommand]
    private async Task TestQrzConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(QrzUsername) || string.IsNullOrWhiteSpace(QrzPassword))
        {
            _dialogService.ShowError("Enter both a QRZ username and password before testing.");
            return;
        }

        await _credentialStore.SaveAsync(QrzLookupService.CredentialKey, QrzUsername, QrzPassword);
        IsQrzConfigured = true;

        var result = await _qrzLookupService.LookupAsync(TestLookupCallsign);
        QrzTestedGood = result.Found;
        if (result.Found)
            _dialogService.ShowInfo("QRZ.com connection successful.");
        else
            _dialogService.ShowError($"QRZ.com test failed: {result.Error ?? "unknown error"}");
    }

    [RelayCommand]
    private async Task SaveQrzCqCredentialsAsync()
    {
        if (string.IsNullOrWhiteSpace(QrzCqUsername) || string.IsNullOrWhiteSpace(QrzCqPassword))
        {
            _dialogService.ShowError("Enter both a QRZCQ username and password.");
            return;
        }

        await _credentialStore.SaveAsync(QrzCqLookupService.CredentialKey, QrzCqUsername, QrzCqPassword);
        IsQrzCqConfigured = true;
        QrzCqTestedGood = false;
        _dialogService.ShowInfo("QRZCQ credentials saved.");
    }

    [RelayCommand]
    private async Task ClearQrzCqCredentialsAsync()
    {
        await _credentialStore.DeleteAsync(QrzCqLookupService.CredentialKey);
        QrzCqUsername = string.Empty;
        QrzCqPassword = string.Empty;
        IsQrzCqConfigured = false;
        QrzCqTestedGood = false;
    }

    /// <summary>Mirror of TestQrzConnectionAsync for QRZCQ.</summary>
    [RelayCommand]
    private async Task TestQrzCqConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(QrzCqUsername) || string.IsNullOrWhiteSpace(QrzCqPassword))
        {
            _dialogService.ShowError("Enter both a QRZCQ username and password before testing.");
            return;
        }

        await _credentialStore.SaveAsync(QrzCqLookupService.CredentialKey, QrzCqUsername, QrzCqPassword);
        IsQrzCqConfigured = true;

        var result = await _qrzCqLookupService.LookupAsync(TestLookupCallsign);
        QrzCqTestedGood = result.Found;
        if (result.Found)
            _dialogService.ShowInfo("QRZCQ.com connection successful.");
        else
            _dialogService.ShowError($"QRZCQ.com test failed: {result.Error ?? "unknown error"}");
    }

    partial void OnCatSourceChanged(CatSource value) => _settings.CatSource = value;

    partial void OnLaunchRigctldAutomaticallyChanged(bool value) => _settings.LaunchRigctldAutomatically = value;

    partial void OnActiveRadioIndexChanged(int value) => _settings.ActiveRadioIndex = value;

    /// <summary>The single "Save Settings" button on the CAT Control window: persists both the Hamlib
    /// radio profiles + rigctld path/port AND the Internet Control host/port + (optional) password in one
    /// click, regardless of which CAT source is currently selected. CatSource itself, ActiveRadioIndex,
    /// and Launch-rigctld persist immediately via their own change handlers, so they aren't repeated here.</summary>
    [RelayCommand]
    private async Task SaveCatSettingsAsync()
    {
        for (int i = 0; i < RadioProfileEditors.Count && i < _settings.RadioProfiles.Count; i++)
        {
            RadioProfileEditors[i].ApplyTo(_settings.RadioProfiles[i]);
        }
        _settings.SaveRadioProfiles();

        _settings.RigctldExecutablePath = string.IsNullOrWhiteSpace(RigctldExecutablePath) ? "rigctld.exe" : RigctldExecutablePath;
        _settings.RigctldTcpPort = int.TryParse(RigctldTcpPort, out var rigctldPort) ? rigctldPort : _settings.RigctldTcpPort;

        _settings.InternetRadioHost = InternetRadioHost?.Trim() ?? string.Empty;
        _settings.InternetRadioPort = int.TryParse(InternetRadioPortText, out var netPort) ? netPort : 9200;
        // Host/port save regardless of whether a password was entered -- unlike QRZ's all-or-nothing rule,
        // the K4 protocol needs no password to connect, so only the encrypted credential write itself is
        // gated on a non-blank password (paired with a fixed placeholder username, no username concept).
        if (!string.IsNullOrWhiteSpace(InternetRadioPassword))
        {
            await _credentialStore.SaveAsync(InternetRadioCredentialKey, "radio", InternetRadioPassword);
            IsInternetRadioConfigured = true;
        }
        InternetRadioPassword = string.Empty;

        _dialogService.ShowInfo("CAT settings saved.");
    }

    /// <summary>The single "Test Connection" button: tests whichever CAT source is currently selected
    /// (USB/Hamlib or Internet/K4). Off = nothing to test.</summary>
    [RelayCommand]
    private async Task TestCatConnectionAsync()
    {
        switch (CatSource)
        {
            case CatSource.Usb:
                await TestHamlibConnectionAsync();
                break;
            case CatSource.Internet:
                await TestInternetConnectionAsync();
                break;
            default:
                _dialogService.ShowError("Select a CAT source (USB or Internet) first.");
                break;
        }
    }

    private async Task TestHamlibConnectionAsync()
    {
        var (success, error) = await _rigCoordinator.ConnectAsync();
        if (success)
        {
            await _rigCoordinator.DisconnectAsync();
            _dialogService.ShowInfo("CAT connection succeeded.");
        }
        else
        {
            _dialogService.ShowError($"CAT connection failed: {error}");
        }
    }

    /// <summary>Live end-to-end check of the Internet Control path: opens a fresh K4 TCP connection to the
    /// configured host/port, reads the radio's current frequency/mode once, then disconnects. Reports what
    /// it read so the operator can confirm it's really talking to their radio, not just reaching a socket.</summary>
    private async Task TestInternetConnectionAsync()
    {
        string host = InternetRadioHost?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            _dialogService.ShowError("Enter the radio's host or IP address first.");
            return;
        }
        int port = int.TryParse(InternetRadioPortText, out var p) ? p : 9200;

        await using var client = new K4CatClient();
        var connect = await client.ConnectAsync(host, port);
        if (!connect.Success)
        {
            _dialogService.ShowError($"Could not connect to {host}:{port}\n{connect.Error}");
            return;
        }

        var poll = await client.PollAsync();
        await client.DisconnectAsync();

        if (poll.Success)
        {
            string mode = poll.MappedMode ?? "unknown mode";
            string band = poll.Band is not null ? $", {poll.Band}" : string.Empty;
            _dialogService.ShowInfo($"Connected to {host}:{port}.\nRadio reports {poll.FrequencyMhz:0.000000} MHz ({mode}{band}).");
        }
        else
        {
            _dialogService.ShowInfo($"Connected to {host}:{port}, but reading the radio's status failed:\n{poll.Error}");
        }
    }

    partial void OnGridTrackerEnabledChanged(bool value) => _settings.GridTrackerEnabled = value;

    /// <summary>Unlike CAT (which only connects when the user later clicks "Connect"), this checkbox
    /// takes effect immediately -- there's no separate connect step for a UDP listener.</summary>
    partial void OnWsjtxEnabledChanged(bool value)
    {
        _settings.WsjtxEnabled = value;
        _wsjtxListener.ApplyEnabledState();
    }

    [RelayCommand]
    private void SaveGridTrackerSettings()
    {
        ApplyGridTrackerHostAndPort();
        _dialogService.ShowInfo("GridTracker2 settings saved.");
    }

    /// <summary>Sends a synthetic test QSO regardless of whether GridTracker broadcasting is enabled,
    /// so the user can confirm GridTracker2 is receiving packets before turning the feature on.</summary>
    [RelayCommand]
    private void SendGridTrackerTestPacket()
    {
        ApplyGridTrackerHostAndPort();
        _gridTrackerBroadcast.SendTestPacket();
        _dialogService.ShowInfo($"Test QSO sent to {_settings.GridTrackerHost}:{_settings.GridTrackerPort}.");
    }

    private void ApplyGridTrackerHostAndPort()
    {
        _settings.GridTrackerHost = string.IsNullOrWhiteSpace(GridTrackerHost) ? "127.0.0.1" : GridTrackerHost;
        _settings.GridTrackerPort = int.TryParse(GridTrackerPort, out var port) ? port : _settings.GridTrackerPort;
    }
}
