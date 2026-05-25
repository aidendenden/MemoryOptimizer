using System.Runtime.InteropServices;

namespace MemoryOptimizer;

internal static class MemoryOptimizerEngine
{
    public static void Optimize(MemoryOptimizationScope scope)
    {
        NativeMethods.SetPrivilege(NativeMethods.SePrivilege.SeProfileSingleProcessPrivilege, true);
        NativeMethods.SetPrivilege(NativeMethods.SePrivilege.SeIncreaseQuotaPrivilege, true);

        if (scope.HasFlag(MemoryOptimizationScope.EmptyWorkingSets))
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.EmptyWorkingSets);

        if (scope.HasFlag(MemoryOptimizationScope.FlushFileCache))
            FlushFileCache();

        if (scope.HasFlag(MemoryOptimizationScope.FlushModifiedList))
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.FlushModifiedList);

        if (scope.HasFlag(MemoryOptimizationScope.PurgeStandbyList))
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.PurgeStandbyList);

        if (scope.HasFlag(MemoryOptimizationScope.PurgeLowPriorityStandbyList))
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.PurgeLowPriorityStandbyList);

        if (scope.HasFlag(MemoryOptimizationScope.RegistryReconciliation))
            NativeMethods.SetSystemInformation(
                NativeMethods.SystemInformationClass.SystemRegistryReconciliationInformation,
                IntPtr.Zero,
                0);

        if (scope.HasFlag(MemoryOptimizationScope.CombinePhysicalMemory))
            ExecuteStructureOperation(
                new NativeMethods.MemoryCombineInformationEx(),
                NativeMethods.SystemInformationClass.SystemCombinePhysicalMemoryInformation);
    }

    private static void ExecuteMemoryListCommand(NativeMethods.MemoryListCommand command)
    {
        var value = (int)command;
        var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
        try
        {
            NativeMethods.SetSystemInformation(
                NativeMethods.SystemInformationClass.SystemMemoryListInformation,
                handle.AddrOfPinnedObject(),
                sizeof(int));
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    private static void FlushFileCache()
    {
        var max = UIntPtr.Size == 8 ? new UIntPtr(ulong.MaxValue) : new UIntPtr(uint.MaxValue);
        var info = new NativeMethods.SystemFileCacheInformation
        {
            MinimumWorkingSet = max,
            MaximumWorkingSet = max
        };

        ExecuteStructureOperation(
            info,
            NativeMethods.SystemInformationClass.SystemFileCacheInformationEx);
    }

    private static void ExecuteStructureOperation<T>(
        T structure,
        NativeMethods.SystemInformationClass informationClass)
    {
        var handle = GCHandle.Alloc(structure, GCHandleType.Pinned);
        try
        {
            NativeMethods.SetSystemInformation(
                informationClass,
                handle.AddrOfPinnedObject(),
                (uint)Marshal.SizeOf<T>());
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }
}
