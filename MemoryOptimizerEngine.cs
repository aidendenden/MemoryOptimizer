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

        if (Includes(scope, MemoryOptimizationScope.EmptyWorkingSets))
        {
            Checkpoint("清理进程工作集", cancellationToken, progress);
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.EmptyWorkingSets);
        }

        if (Includes(scope, MemoryOptimizationScope.FlushFileCache))
        {
            Checkpoint("刷新文件缓存", cancellationToken, progress);
            FlushFileCache();
        }

        if (Includes(scope, MemoryOptimizationScope.FlushModifiedList))
        {
            Checkpoint("刷新已修改页面列表", cancellationToken, progress);
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.FlushModifiedList);
        }

        if (Includes(scope, MemoryOptimizationScope.PurgeStandbyList))
        {
            Checkpoint("清理备用页面列表", cancellationToken, progress);
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.PurgeStandbyList);
        }

        if (Includes(scope, MemoryOptimizationScope.PurgeLowPriorityStandbyList))
        {
            Checkpoint("清理低优先级备用页面", cancellationToken, progress);
            ExecuteMemoryListCommand(NativeMethods.MemoryListCommand.PurgeLowPriorityStandbyList);
        }

        if (Includes(scope, MemoryOptimizationScope.RegistryReconciliation))
        {
            Checkpoint("同步注册表内存", cancellationToken, progress);
            NativeMethods.SetSystemInformation(
                NativeMethods.SystemInformationClass.SystemRegistryReconciliationInformation,
                IntPtr.Zero,
                0);
        }

        if (Includes(scope, MemoryOptimizationScope.CombinePhysicalMemory))
        {
            Checkpoint("合并物理内存页面", cancellationToken, progress);
            ExecuteStructureOperation(
                new NativeMethods.MemoryCombineInformationEx(),
                NativeMethods.SystemInformationClass.SystemCombinePhysicalMemoryInformation);
        }
    }

    private static bool Includes(MemoryOptimizationScope scope, MemoryOptimizationScope flag) =>
        (scope & flag) != 0;

    private static void Checkpoint(
        string name,
        CancellationToken cancellationToken,
        Action<string>? progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Invoke(name);
    }

    private static unsafe void ExecuteMemoryListCommand(NativeMethods.MemoryListCommand command)
    {
        var value = (int)command;
        NativeMethods.SetSystemInformation(
            NativeMethods.SystemInformationClass.SystemMemoryListInformation,
            (IntPtr)(&value),
            sizeof(int));
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

    private static unsafe void ExecuteStructureOperation<T>(
        T structure,
        NativeMethods.SystemInformationClass informationClass)
        where T : unmanaged
    {
        NativeMethods.SetSystemInformation(
            informationClass,
            (IntPtr)(&structure),
            (uint)sizeof(T));
    }
}
