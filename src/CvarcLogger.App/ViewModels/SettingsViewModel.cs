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

    [ObservableProperty] private LookupServicePreference preferredLookupService;
    [ObservableProperty] private string qrzUsername = string.Empty;
    [ObservableProperty] private string qrzPassword = string.Empty;
    [ObservableProperty] private bool isQrzConfigured;
    [ObservableProperty] private string qrzCqUsername = string.Empty;
    [ObservableProperty] private string qrzCqPassword = string.Empty;
    [ObservableProperty] private bool isQrzCqConfigured;

    [ObservableProperty] private bool catEnabled;
    [ObservableProperty] private bool launchRigctldAutomatically;
    [ObservableProperty] private string rigctldExecutablePath = "rigctld.exe";
    [ObservableProperty] private string rigctldTcpPort = "4532";
    [ObservableProperty] private int activeRadioIndex;

    public ObservableCollection<string> RadioNames { get; } = new();
    public ObservableCollection<RadioProfileEditorViewModel> RadioProfileEditors { get; } = new();
    public ObservableCollection<HamlibRigInfo> AvailableRigs { get; } = new();
    public ObservableCollection<string> AvailableComPorts { get; } = new();

    public SettingsViewModel(
        SettingsService settings,
        ICredentialStore credentialStore,
        DialogService dialogService,
        RigControlCoordinator rigCoordinator,
        HamlibRigCatalog rigCatalog)
    {
        _settings = settings;
        _credentialStore = credentialStore;
        _dialogService = dialogService;
        _rigCoordinator = rigCoordinator;
        _rigCatalog = rigCatalog;
        preferredLookupService = _settings.PreferredLookupService;

        catEnabled = _settings.CatEnabled;
        launchRigctldAutomatically = _settings.LaunchRigctldAutomatically;
        rigctldExecutablePath = _settings.RigctldExecutablePath;
        rigctldTcpPort = _settings.RigctldTcpPort.ToString();
        activeRadioIndex = _settings.ActiveRadioIndex;
        for (int i = 0; i < _settings.RadioProfiles.Count; i++)
        {
            var profile = _settings.RadioProfiles[i];
            string slotLabel = $"Radio {i + 1}";
            RadioNames.Add(slotLabel);
            RadioProfileEditors.Add(new RadioProfileEditorViewModel(profile, slotLabel, AvailableRigs, AvailableComPorts));
        }
    }

    public async Task InitializeAsync()
    {
        var creds = await _credentialStore.LoadAsync(QrzLookupService.CredentialKey);
        IsQrzConfigured = creds is not null;
        if (creds is not null) QrzUsername = creds.Value.Username;

        var qrzCqCreds = await _credentialStore.LoadAsync(QrzCqLookupService.CredentialKey);
        IsQrzCqConfigured = qrzCqCreds is not null;
        if (qrzCqCreds is not null) QrzCqUsername = qrzCqCreds.Value.Username;

        var rigs = await _rigCatalog.GetRigsAsync(_settings.RigctldExecutablePath);
        AvailableRigs.Clear();
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
        QrzPassword = string.Empty;
        IsQrzConfigured = true;
        _dialogService.ShowInfo("QRZ credentials saved.");
    }

    [RelayCommand]
    private async Task ClearQrzCredentialsAsync()
    {
        await _credentialStore.DeleteAsync(QrzLookupService.CredentialKey);
        QrzUsername = string.Empty;
        QrzPassword = string.Empty;
        IsQrzConfigured = false;
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
        QrzCqPassword = string.Empty;
        IsQrzCqConfigured = true;
        _dialogService.ShowInfo("QRZCQ credentials saved.");
    }

    [RelayCommand]
    private async Task ClearQrzCqCredentialsAsync()
    {
        await _credentialStore.DeleteAsync(QrzCqLookupService.CredentialKey);
        QrzCqUsername = string.Empty;
        QrzCqPassword = string.Empty;
        IsQrzCqConfigured = false;
    }

    partial void OnPreferredLookupServiceChanged(LookupServicePreference value)
    {
        _settings.PreferredLookupService = value;
    }

    partial void OnCatEnabledChanged(bool value) => _settings.CatEnabled = value;

    partial void OnLaunchRigctldAutomaticallyChanged(bool value) => _settings.LaunchRigctldAutomatically = value;

    partial void OnActiveRadioIndexChanged(int value) => _settings.ActiveRadioIndex = value;

    [RelayCommand]
    private void SaveRadioSettings()
    {
        for (int i = 0; i < RadioProfileEditors.Count && i < _settings.RadioProfiles.Count; i++)
        {
            RadioProfileEditors[i].ApplyTo(_settings.RadioProfiles[i]);
        }
        _settings.SaveRadioProfiles();

        _settings.RigctldExecutablePath = string.IsNullOrWhiteSpace(RigctldExecutablePath) ? "rigctld.exe" : RigctldExecutablePath;
        _settings.RigctldTcpPort = int.TryParse(RigctldTcpPort, out var port) ? port : _settings.RigctldTcpPort;

        _dialogService.ShowInfo("Radio settings saved.");
    }

    [RelayCommand]
    private async Task TestCatConnectionAsync()
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
}
