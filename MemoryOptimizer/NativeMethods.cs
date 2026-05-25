using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MemoryOptimizer;

internal static partial class NativeMethods
{
    internal enum SePrivilege : uint
    {
        SeIncreaseQuotaPrivilege = 5,
        SeProfileSingleProcessPrivilege = 13
    }

    internal enum SystemInformationClass
    {
        SystemMemoryListInformation = 80,
        SystemFileCacheInformationEx = 81,
        SystemCombinePhysicalMemoryInformation = 130,
        SystemRegistryReconciliationInformation = 155
    }

    internal enum MemoryListCommand
    {
        EmptyWorkingSets = 2,
        FlushModifiedList = 3,
        PurgeStandbyList = 4,
        PurgeLowPriorityStandbyList = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemFileCacheInformation
    {
        public UIntPtr CurrentSize;
        public UIntPtr PeakSize;
        public UIntPtr PageFaultCount;
        public UIntPtr MinimumWorkingSet;
        public UIntPtr MaximumWorkingSet;
        public UIntPtr CurrentSizeIncludingTransitionInPages;
        public UIntPtr PeakSizeIncludingTransitionInPages;
        public UIntPtr TransitionRePurposeCount;
        public UIntPtr Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryCombineInformationEx
    {
        public IntPtr Handle;
        public UIntPtr PagesCombined;
        public uint Flags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("ntdll.dll")]
    private static extern uint RtlAdjustPrivilege(
        SePrivilege privilege,
        [MarshalAs(UnmanagedType.U1)] bool enable,
        [MarshalAs(UnmanagedType.U1)] bool currentThread,
        [MarshalAs(UnmanagedType.U1)] out bool enabled);

    [DllImport("ntdll.dll")]
    private static extern ulong RtlNtStatusToDosError(uint status);

    [DllImport("ntdll.dll")]
    private static extern uint NtSetSystemInformation(
        SystemInformationClass systemInformationClass,
        IntPtr systemInformation,
        uint systemInformationLength);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EmptyWorkingSet(IntPtr hProcess);

    public static MemoryStatusEx GetMemoryStatus()
    {
        var status = new MemoryStatusEx
        {
            dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return status;
    }

    public static bool SetPrivilege(SePrivilege privilege, bool enabled)
    {
        var result = RtlAdjustPrivilege(privilege, enabled, currentThread: false, out var previousState);
        ThrowIfNtError(result);
        return previousState;
    }

    public static void SetSystemInformation(
        SystemInformationClass informationClass,
        IntPtr information,
        uint informationLength)
    {
        var result = NtSetSystemInformation(informationClass, information, informationLength);
        ThrowIfNtError(result);
    }

    private static void ThrowIfNtError(uint status)
    {
        if (status == 0) return;
        throw new Win32Exception((int)RtlNtStatusToDosError(status));
    }
}
