namespace CvarcLogger.Core.Abstractions;

/// <summary>Stores third-party service credentials (e.g. QRZ username/password) encrypted at rest.
/// Never persists to the SQLite database or to logs.</summary>
public interface ICredentialStore
{
    Task SaveAsync(string key, string username, string password, CancellationToken ct = default);
    Task<(string Username, string Password)?> LoadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
