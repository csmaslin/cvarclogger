using System.ComponentModel;
using System.Diagnostics;
using CvarcLogger.Core.Rig;
using Serilog;

namespace CvarcLogger.App.Platform;

/// <summary>Launches and kills rigctld.exe as a background process. Best-effort: never throws,
/// since a launch/kill failure should surface as a status message, not crash the app.</summary>
public class RigctldProcessManager : IDisposable
{
    private Process? _process;
    private readonly JobObject _jobObject = new();
    private bool _stoppingIntentionally;

    public bool IsRunning => _process is { HasExited: false };

    public Task<(bool Success, string? Error)> StartAsync(RadioProfile profile, string rigctldPath, int tcpPort)
    {
        if (IsRunning) return Task.FromResult<(bool, string?)>((true, null));

        _stoppingIntentionally = false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = rigctldPath,
                Arguments = RigctldLaunchArgs.Build(profile, tcpPort),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            _process = Process.Start(psi);
            if (_process is null) return Task.FromResult<(bool, string?)>((false, "Failed to start rigctld."));

            // Capture this specific instance for the closures below — _process is a mutable field that
            // Stop() nulls out, and a later StartAsync() call could point it at a different process
            // entirely by the time these callbacks fire.
            var started = _process;

            // RedirectStandardOutput/Error above gives rigctld's stdout/stderr a fixed-size OS pipe
            // buffer (a few KB). Nothing reading from it means that once rigctld writes enough — and
            // real Hamlib rig backends can log heavily on any serial retry, timeout, or NACK from the
            // radio — the pipe fills and rigctld blocks on its own write() call, hanging indefinitely.
            // From the outside this looks exactly like "CAT stops responding shortly after connecting":
            // the poll loop's reads start timing out because rigctld itself is frozen, not disconnected.
            // BeginOutputReadLine/BeginErrorReadLine drain the pipes continuously via async callbacks,
            // which both prevents the deadlock and gives us rigctld's own diagnostic output in the log.
            // Information, not Debug: the default MinimumLevel is Information (see App.xaml.cs), and
            // rigctld is silent under healthy operation (verified empirically against a running
            // instance) — so this only produces log volume exactly when something's actually wrong,
            // which is when it's most needed.
            started.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Log.Information("rigctld [{Pid}] stdout: {Line}", started.Id, e.Data);
            };
            started.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Log.Information("rigctld [{Pid}] stderr: {Line}", started.Id, e.Data);
            };
            started.BeginOutputReadLine();
            started.BeginErrorReadLine();

            started.EnableRaisingEvents = true;
            started.Exited += (_, _) =>
            {
                // Skip the warning when this is our own intentional Stop()/Kill() — only rigctld dying
                // on its own (crash, COM port failure, etc.) is the diagnostically interesting case.
                if (_stoppingIntentionally) return;
                try
                {
                    Log.Warning("rigctld [{Pid}] exited unexpectedly (code {ExitCode}).", started.Id, started.ExitCode);
                }
                catch (InvalidOperationException)
                {
                    // Disposed before we could read ExitCode — nothing more to report.
                }
            };

            // Safety net: guarantees the OS kills this process if CvarcLogger exits abnormally
            // (crash, "End Task") and Stop() below never gets to run. See JobObject's own doc comment.
            _jobObject.Assign(started);

            Log.Information("Launched rigctld (PID {Pid}) for {Radio} on {ComPort}: {Path} {Args}",
                started.Id, profile.Name, profile.ComPort, rigctldPath, psi.Arguments);

            return Task.FromResult<(bool, string?)>((true, null));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or System.IO.FileNotFoundException)
        {
            return Task.FromResult<(bool, string?)>((false, $"Could not launch rigctld: {ex.Message}"));
        }
    }

    public void Stop()
    {
        if (_process is null) return;
        _stoppingIntentionally = true;
        try
        {
            if (!_process.HasExited)
            {
                Log.Information("Stopping rigctld (PID {Pid}).", _process.Id);
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(2000);
            }
        }
        catch
        {
            // Best-effort on shutdown — never throw.
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _jobObject.Dispose();
    }
}
