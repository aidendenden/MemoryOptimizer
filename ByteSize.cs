namespace MemoryOptimizer;

internal static class ByteSize
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(ulong bytes)
    {
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {Units[unit]}" : $"{value:0.##} {Units[unit]}";
    }
}
