using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CvarcLogger.App.Platform;

/// <summary>Wraps a Windows Job Object configured to kill every process assigned to it the instant
/// the job's last handle closes. Assigning rigctld.exe here guarantees the OS cleans it up even if
/// CvarcLogger exits abnormally — crash, Task Manager "End Task", power loss to the desktop session —
/// none of which run RigctldProcessManager.Stop() or any other managed cleanup code. Best-effort:
/// if job creation fails for any reason, rigctld simply falls back to relying on the explicit
/// Stop() call on graceful shutdown, same as before this existed.</summary>
public sealed class JobObject : IDisposable
{
    private readonly SafeFileHandle? _handle;

    public JobObject()
    {
        SafeFileHandle handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid) return;

        var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new NativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        int length = Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        IntPtr infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, infoPtr, false);
            if (NativeMethods.SetInformationJobObject(
                    handle, NativeMethods.JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)length))
            {
                _handle = handle;
            }
            else
            {
                handle.Dispose();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }
    }

    public void Assign(Process process)
    {
        if (_handle is null) return;
        try
        {
            NativeMethods.AssignProcessToJobObject(_handle, process.Handle);
        }
        catch (Win32Exception)
        {
            // Best-effort safety net only — explicit Stop() on graceful shutdown is unaffected.
        }
    }

    public void Dispose() => _handle?.Dispose();

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AssignProcessToJobObject(SafeFileHandle hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetInformationJobObject(
            SafeFileHandle hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        public enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    }
}
