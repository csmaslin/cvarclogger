using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcCellLog.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Lookup;

namespace CvarcCellLog.ViewModels;

/// <summary>Adapted from the WPF app's SettingsViewModel/SettingsWindow -- Callsign Lookup section only
/// (Preferred service + QRZ/QRZCQ credentials). Radio Control/GridTracker/WSJT-X stay out of scope,
/// unchanged from the Milestone 1 decision. Password fields use MAUI Entry's native IsPassword instead of
/// the WPF app's PasswordBox+show/hide-toggle -- no separate "Show password" affordance needed here.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ICredentialStore _credentialStore;

    [ObservableProperty] private LookupServicePreference preferredLookupService;

    [ObservableProperty] private string qrzUsername = string.Empty;
    [ObservableProperty] private string qrzPassword = string.Empty;
    [ObservableProperty] private string qrzStatusText = "Not Configured";
    [ObservableProperty] private bool qrzPasswordHidden = true;

    [ObservableProperty] private string qrzCqUsername = string.Empty;
    [ObservableProperty] private string qrzCqPassword = string.Empty;
    [ObservableProperty] private string qrzCqStatusText = "Not Configured";
    [ObservableProperty] private bool qrzCqPasswordHidden = true;

    public Array LookupServicePreferences { get; } = Enum.GetValues(typeof(LookupServicePreference));

    public SettingsViewModel(ICredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
    }

    public async Task InitializeAsync()
    {
        PreferredLookupService = LookupCoordinator.PreferredService;

        var qrzCreds = await _credentialStore.LoadAsync(QrzLookupService.CredentialKey);
        QrzStatusText = qrzCreds is not null ? "Configured" : "Not Configured";
        if (qrzCreds is not null) QrzUsername = qrzCreds.Value.Username;

        var qrzCqCreds = await _credentialStore.LoadAsync(QrzCqLookupService.CredentialKey);
        QrzCqStatusText = qrzCqCreds is not null ? "Configured" : "Not Configured";
        if (qrzCqCreds is not null) QrzCqUsername = qrzCqCreds.Value.Username;
    }

    partial void OnPreferredLookupServiceChanged(LookupServicePreference value) =>
        LookupCoordinator.PreferredService = value;

    /// <summary>Saving clears the plaintext field (see SaveQrzCredentialsAsync), so revealing it
    /// afterward has nothing to show unless we re-fetch the stored value from the credential store --
    /// otherwise "Show" would just display blank even though a password is configured.</summary>
    [RelayCommand]
    private async Task ToggleQrzPasswordVisibilityAsync()
    {
        if (QrzPasswordHidden && string.IsNullOrEmpty(QrzPassword))
        {
            var creds = await _credentialStore.LoadAsync(QrzLookupService.CredentialKey);
            if (creds is not null) QrzPassword = creds.Value.Password;
        }

        QrzPasswordHidden = !QrzPasswordHidden;
    }

    [RelayCommand]
    private async Task ToggleQrzCqPasswordVisibilityAsync()
    {
        if (QrzCqPasswordHidden && string.IsNullOrEmpty(QrzCqPassword))
        {
            var creds = await _credentialStore.LoadAsync(QrzCqLookupService.CredentialKey);
            if (creds is not null) QrzCqPassword = creds.Value.Password;
        }

        QrzCqPasswordHidden = !QrzCqPasswordHidden;
    }

    [RelayCommand]
    private async Task SaveQrzCredentialsAsync()
    {
        if (string.IsNullOrWhiteSpace(QrzUsername) || string.IsNullOrWhiteSpace(QrzPassword)) return;

        await _credentialStore.SaveAsync(QrzLookupService.CredentialKey, QrzUsername, QrzPassword);
        QrzPassword = string.Empty;
        QrzStatusText = "Configured";
    }

    [RelayCommand]
    private async Task ClearQrzCredentialsAsync()
    {
        await _credentialStore.DeleteAsync(QrzLookupService.CredentialKey);
        QrzUsername = string.Empty;
        QrzPassword = string.Empty;
        QrzStatusText = "Not Configured";
    }

    [RelayCommand]
    private async Task SaveQrzCqCredentialsAsync()
    {
        if (string.IsNullOrWhiteSpace(QrzCqUsername) || string.IsNullOrWhiteSpace(QrzCqPassword)) return;

        await _credentialStore.SaveAsync(QrzCqLookupService.CredentialKey, QrzCqUsername, QrzCqPassword);
        QrzCqPassword = string.Empty;
        QrzCqStatusText = "Configured";
    }

    [RelayCommand]
    private async Task ClearQrzCqCredentialsAsync()
    {
        await _credentialStore.DeleteAsync(QrzCqLookupService.CredentialKey);
        QrzCqUsername = string.Empty;
        QrzCqPassword = string.Empty;
        QrzCqStatusText = "Not Configured";
    }
}
