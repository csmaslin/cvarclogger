using Serilog;

namespace CvarcLogger.App.Services;

/// <summary>Wires K4CatClient + SettingsService together so the entry-form ViewModel has one
/// connect/disconnect/poll call to make for network (Internet Control) CAT, exactly mirroring
/// RigControlCoordinator's role for the Hamlib/rigctld serial path. Reads host/port from Settings at
/// connect time so editing them takes effect on the next connect without an app restart. The K4 protocol
/// is read-only status (frequency/mode/power); this never sends a command that changes the radio.</summary>
public class InternetCatCoordinator : IAsyncDisposable
{
    private readonly SettingsService _settings;
    private readonly K4CatClient _client = new();

    public InternetCatCoordinator(SettingsService settings)
    {
        _settings = settings;
    }

    public K4ConnectionState State => _client.State;

    public async Task<(bool Success, string? Error)> ConnectAsync(CancellationToken ct = default)
    {
        if (!_settings.InternetRadioEnabled)
            return (false, "Internet Control (CAT) is disabled in Settings.");

        string host = _settings.InternetRadioHost?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host))
            return (false, "No radio host/IP configured for Internet Control (set it under CAT Control).");

        var result = await _client.ConnectAsync(host, _settings.InternetRadioPort, ct);
        if (result.Success)
            Log.Information("Internet CAT connected to {Host}:{Port}.", host, _settings.InternetRadioPort);
        else
            Log.Warning("Internet CAT connect to {Host}:{Port} failed: {Error}", host, _settings.InternetRadioPort, result.Error);
        return (result.Success, result.Error);
    }

    public Task<K4PollResult> PollAsync(CancellationToken ct = default) => _client.PollAsync(ct);

    public Task DisconnectAsync() => _client.DisconnectAsync();

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
