using CvarcLogger.Core.Abstractions;

namespace CvarcCellLog.Services;

/// <summary>MAUI-side ICredentialStore, backed by SecureStorage instead of the WPF app's DPAPI file
/// (DpapiCredentialStore) -- same interface, same credential keys ("QRZ"/"QRZCQ", see
/// QrzLookupService.CredentialKey/QrzCqLookupService.CredentialKey), no Core changes needed. Username and
/// password are stored as two separate SecureStorage entries per credential key.</summary>
public class SecureStorageCredentialStore : ICredentialStore
{
    private static string UsernameKey(string key) => $"{key}_username";
    private static string PasswordKey(string key) => $"{key}_password";

    public async Task SaveAsync(string key, string username, string password, CancellationToken ct = default)
    {
        await SecureStorage.Default.SetAsync(UsernameKey(key), username);
        await SecureStorage.Default.SetAsync(PasswordKey(key), password);
    }

    public async Task<(string Username, string Password)?> LoadAsync(string key, CancellationToken ct = default)
    {
        string? username = await SecureStorage.Default.GetAsync(UsernameKey(key));
        string? password = await SecureStorage.Default.GetAsync(PasswordKey(key));
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return null;
        return (username, password);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        SecureStorage.Default.Remove(UsernameKey(key));
        SecureStorage.Default.Remove(PasswordKey(key));
        return Task.CompletedTask;
    }
}
