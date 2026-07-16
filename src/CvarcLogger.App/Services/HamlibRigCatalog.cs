using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CvarcLogger.Core.Rig;

namespace CvarcLogger.App.Services;

/// <summary>Runs `rigctld --list` once and caches the result — that's every rig model the bundled (or
/// user-configured) Hamlib build knows about, used to populate the radio picker in Settings instead of
/// making the user look up a numeric model ID by hand. Best-effort: an unreadable/missing rigctld just
/// yields an empty list, so the raw Model ID field in Settings remains the fallback.</summary>
public class HamlibRigCatalog
{
    private IReadOnlyList<HamlibRigInfo>? _cache;

    public async Task<IReadOnlyList<HamlibRigInfo>> GetRigsAsync(string rigctldPath, CancellationToken ct = default)
    {
        if (_cache is not null) return _cache;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = rigctldPath,
                Arguments = "--list",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return _cache = Array.Empty<HamlibRigInfo>();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct); // drain so the process can't block on a full pipe
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(ct);

            _cache = HamlibRigListParser.Parse(stdoutTask.Result);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            _cache = Array.Empty<HamlibRigInfo>();
        }

        return _cache;
    }
}
