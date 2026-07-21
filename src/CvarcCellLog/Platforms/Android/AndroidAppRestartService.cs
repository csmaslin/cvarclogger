using Android.Content;
using CvarcCellLog.Services;

namespace CvarcCellLog.Platforms.Android;

/// <summary>Android-native equivalent of the WPF app's RestartApplication() (Process.Start + Shutdown) --
/// relaunches MainActivity via a fresh Intent, then kills this process. Standard MAUI/Android
/// self-restart pattern.</summary>
public class AndroidAppRestartService : IAppRestartService
{
    public void Restart()
    {
        var context = global::Android.App.Application.Context;
        var intent = context.PackageManager!.GetLaunchIntentForPackage(context.PackageName!);
        intent!.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
        context.StartActivity(intent);
        global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
    }
}
