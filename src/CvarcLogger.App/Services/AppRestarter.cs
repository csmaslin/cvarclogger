using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CvarcLogger.App.Services;

/// <summary>Relaunches the current executable and shuts down this instance, used after New Log/Open Log
/// switches the active database path -- the app doesn't support hot-swapping its DbContext mid-run.</summary>
public static class AppRestarter
{
    public static void Restart()
    {
        string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CvarcLogger.exe");
        var psi = new ProcessStartInfo { UseShellExecute = true };

        if (Path.GetFileNameWithoutExtension(exePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            // Running via `dotnet run` — relaunch through dotnet against our own assembly rather than
            // starting a bare "dotnet" with no arguments.
            psi.FileName = "dotnet";
            psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "CvarcLogger.dll"));
        }
        else
        {
            psi.FileName = exePath;
        }

        Process.Start(psi);
        Application.Current.Shutdown();
    }
}
