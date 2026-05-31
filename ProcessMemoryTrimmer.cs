namespace MemoryOptimizer;

internal static class ProcessMemoryTrimmer
{
    public static void TrimWorkingSet()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false, compacting: false);
            NativeMethods.EmptyCurrentProcessWorkingSet();
        }
        catch
        {
            // Working-set trimming is best effort; the optimizer itself should never fail because of it.
        }
    }
}
