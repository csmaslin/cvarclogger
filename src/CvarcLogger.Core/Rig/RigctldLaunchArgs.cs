using System.Globalization;

namespace CvarcLogger.Core.Rig;

/// <summary>Builds the rigctld.exe command-line arguments for a radio profile. Kept as a pure
/// function in Core so it's unit-testable even though the actual process launch (App-layer,
/// RigctldProcessManager) isn't.</summary>
public static class RigctldLaunchArgs
{
    public static string Build(RadioProfile profile, int tcpPort) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "-m {0} -r {1} -s {2} -t {3}",
            profile.HamlibModelId, profile.ComPort, profile.BaudRate, tcpPort);
}
