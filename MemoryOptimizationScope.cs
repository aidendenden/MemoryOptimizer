namespace MemoryOptimizer;

[Flags]
public enum MemoryOptimizationScope
{
    None = 0,
    EmptyWorkingSets = 1 << 0,
    FlushFileCache = 1 << 1,
    FlushModifiedList = 1 << 2,
    PurgeStandbyList = 1 << 3,
    PurgeLowPriorityStandbyList = 1 << 4,
    RegistryReconciliation = 1 << 5,
    CombinePhysicalMemory = 1 << 6,

    Recommended = EmptyWorkingSets
                  | FlushModifiedList
                  | PurgeStandbyList
                  | PurgeLowPriorityStandbyList,

    All = EmptyWorkingSets
          | FlushFileCache
          | FlushModifiedList
          | PurgeStandbyList
          | PurgeLowPriorityStandbyList
          | RegistryReconciliation
          | CombinePhysicalMemory
}
