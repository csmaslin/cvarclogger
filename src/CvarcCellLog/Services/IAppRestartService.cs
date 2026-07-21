namespace CvarcCellLog.Services;

/// <summary>Portable equivalent of the WPF app's static RestartApplication() helper (Process.Start +
/// Shutdown, which don't exist on Android) -- relaunches the app then kills the current process.</summary>
public interface IAppRestartService
{
    void Restart();
}
