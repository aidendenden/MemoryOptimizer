using System.Runtime.InteropServices;

namespace MemoryOptimizer;

internal static class MemoryOptimizerEngine
{
    public static void Optimize(
        MemoryOptimizationScope scope,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Invoke("获取系统内存管理权限");
        NativeMethods.SetPrivilege(NativeMethods.SePrivilege.SeProfileSingleProcessPrivilege, true);
        NativeMethods.SetPrivilege(NativeMethods.SePrivilege.SeIncreaseQuotaPrivilege, true);

        if (scope.HasFlag(MemoryOptimizationScope.EmptyWorkingSets))
            ExecuteStep(
                "清理进程工作集",
                cancellationToken,
                progress,
                () => ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.EmptyWorkingSets));

        if (scope.HasFlag(MemoryOptimizationScope.FlushFileCache))
            ExecuteStep("刷新文件缓存", cancellationToken, progress, FlushFileCache);

        if (scope.HasFlag(MemoryOptimizationScope.FlushModifiedList))
            ExecuteStep(
                "刷新已修改页面列表",
                cancellationToken,
                progress,
                () => ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.FlushModifiedList));

        if (scope.HasFlag(MemoryOptimizationScope.PurgeStandbyList))
            ExecuteStep(
                "清理备用页面列表",
                cancellationToken,
                progress,
                () => ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.PurgeStandbyList));

        if (scope.HasFlag(MemoryOptimizationScope.PurgeLowPriorityStandbyList))
            ExecuteStep(
                "清理低优先级备用页面",
                cancellationToken,
                progress,
                () => ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.PurgeLowPriorityStandbyList));

        if (scope.HasFlag(MemoryOptimizationScope.RegistryReconciliation))
            ExecuteStep(
                "同步注册表内存",
                cancellationToken,
                progress,
                () => NativeMethods.SetSystemInformation(
                    NativeMethods.SystemInformationClass.SystemRegistryReconciliationInformation,
                    IntPtr.Zero,
                    0));

        if (scope.HasFlag(MemoryOptimizationScope.CombinePhysicalMemory))
            ExecuteStep(
                "合并物理内存页面",
                cancellationToken,
                progress,
                () => ExecuteStructureOperation(
                    new NativeMethods.MemoryCombineInformationEx(),
                    NativeMethods.SystemInformationClass.SystemCombinePhysicalMemoryInformation));
    }

    private static void ExecuteStep(
        string name,
        CancellationToken cancellationToken,
        Action<string>? progress,
        Action operation)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Invoke(name);
        operation();
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
