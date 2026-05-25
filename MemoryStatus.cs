namespace MemoryOptimizer;

internal readonly record struct MemoryStatus(
    uint LoadPercent,
    ulong TotalPhysicalBytes,
    ulong AvailablePhysicalBytes)
{
    public static MemoryStatus Query()
    {
        var native = NativeMethods.GetMemoryStatus();
        return new MemoryStatus(native.dwMemoryLoad, native.ullTotalPhys, native.ullAvailPhys);
    }
}
