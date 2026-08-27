using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CvarcLogger.Core.Abstractions;

namespace CvarcLogger.App.Platform;

/// <summary>Stores service credentials (e.g. QRZ username/password) DPAPI-encrypted at
/// %LOCALAPPDATA%\CvarcLogger\credentials.dpapi, scoped to the current Windows user. Never plaintext,
/// never in the SQLite database or logs.</summary>
public class DpapiCredentialStore : ICredentialStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DpapiCredentialStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(App.DatabaseDirectory, "credentials.dpapi");
    }

    public async Task SaveAsync(string key, string username, string password, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var all = await LoadAllAsync(ct).ConfigureAwait(false);
            all[key] = new StoredCredential(username, password);
            await WriteAllAsync(all, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(string Username, string Password)?> LoadAsync(string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var all = await LoadAllAsync(ct).ConfigureAwait(false);
            return all.TryGetValue(key, out var cred) ? (cred.Username, cred.Password) : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var all = await LoadAllAsync(ct).ConfigureAwait(false);
            if (all.Remove(key))
            {
                await WriteAllAsync(all, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, StoredCredential>> LoadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, StoredCredential>();

        byte[] encrypted = await File.ReadAllBytesAsync(_filePath, ct).ConfigureAwait(false);
        byte[] plain = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
        string json = Encoding.UTF8.GetString(plain);
        return JsonSerializer.Deserialize<Dictionary<string, StoredCredential>>(json)
               ?? new Dictionary<string, StoredCredential>();
    }

    private async Task WriteAllAsync(Dictionary<string, StoredCredential> all, CancellationToken ct)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(all);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_filePath, encrypted, ct).ConfigureAwait(false);
    }

    private record StoredCredential(string Username, string Password);
}
