using CvarcLogger.App.Platform;
using CvarcLogger.Core.Rig;
using Serilog;

namespace CvarcLogger.App.Services;

/// <summary>Wires IRigControlService + RigctldProcessManager + SettingsService together so the
/// ViewModel has one connect/disconnect/poll call to make. Mirrors LookupCoordinator's role for
/// callsign lookups.</summary>
public class RigControlCoordinator : IAsyncDisposable
{
    private readonly IRigControlService _rig;
    private readonly RigctldProcessManager _processManager;
    private readonly SettingsService _settings;

    public RigControlCoordinator(IRigControlService rig, RigctldProcessManager processManager, SettingsService settings)
    {
        _rig = rig;
        _processManager = processManager;
        _settings = settings;
    }

    public RigConnectionState State => _rig.State;

    /// <summary>Max RF output (watts) of the currently-selected radio profile -- used to turn a poll's
    /// RFPOWER fraction into an estimated TX Power. Read from Settings directly (not just "at connect
    /// time") so editing it while already connected takes effect on the next poll.</summary>
    public int? ActiveRadioMaxPowerWatts => _settings.RadioProfiles.ElementAtOrDefault(_settings.ActiveRadioIndex)?.MaxPowerWatts;

    public async Task<(bool Success, string? Error)> ConnectAsync(CancellationToken ct = default)
    {
        if (!_settings.CatEnabled)
            return (false, "CAT control is disabled in Settings.");

        if (_rig.State == RigConnectionState.Connected)
            await DisconnectAsync();

        var profile = _settings.RadioProfiles.ElementAtOrDefault(_settings.ActiveRadioIndex);
        if (profile is null)
            return (false, "No radio profile selected.");
        if (profile.HamlibModelId <= 0)
            return (false, $"'{profile.Name}' has no Hamlib model ID configured yet (run 'rigctl --list' and fill it in under Settings).");

        if (_settings.LaunchRigctldAutomatically)
        {
            var (started, launchError) = await _processManager.StartAsync(profile, _settings.RigctldExecutablePath, _settings.RigctldTcpPort);
            if (!started)
                return (false, launchError);
        }

        // rigctld can take a few seconds to actually bind its TCP listener on a fresh launch (loading
        // libhamlib, enumerating the COM port, and — the first time Windows sees this exe listen on a
        // port — a Windows Firewall prompt can block it until dismissed). A single fixed-delay attempt
        // was failing ("canceled") whenever startup took longer than that one window, so retry instead.
        RigConnectResult result = RigConnectResult.Failed("Connection attempt did not run.");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(500, ct);
            result = await _rig.ConnectAsync("127.0.0.1", _settings.RigctldTcpPort, ct);
            if (result.Success) break;
            Log.Information("CAT connect attempt {Attempt}/10 to {Radio} failed: {Error}", attempt + 1, profile.Name, result.Error);
        }

        if (!result.Success)
        {
            Log.Warning("CAT connect to {Radio} gave up after 10 attempts: {Error}", profile.Name, result.Error);
            if (_settings.LaunchRigctldAutomatically) _processManager.Stop();
            string hint = _settings.LaunchRigctldAutomatically
                ? " If a Windows Firewall/Defender prompt appeared for rigctld.exe, allow it and try again."
                : "";
            return (false, result.Error + hint);
        }

        Log.Information("CAT connected to {Radio}.", profile.Name);
        return (true, null);
    }

    public async Task DisconnectAsync()
    {
        // Always stop whatever RigctldProcessManager is tracking, regardless of the current
        // LaunchRigctldAutomatically setting — if it was checked when we launched rigctld but got
        // unchecked before disconnecting, that process must still be cleaned up here rather than
        // silently abandoned. RigctldProcessManager.Stop() is already a safe no-op if it never
        // started anything. The try/finally guarantees this runs even if the CAT disconnect itself
        // misbehaves.
        try
        {
            await _rig.DisconnectAsync();
        }
        finally
        {
            _processManager.Stop();
        }
    }

    public Task<RigPollResult> PollAsync(CancellationToken ct = default) => _rig.PollAsync(ct);

    public async ValueTask DisposeAsync()
    {
        await _rig.DisposeAsync();
        _processManager.Dispose();
    }
}
