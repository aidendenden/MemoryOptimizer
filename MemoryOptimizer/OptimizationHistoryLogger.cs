using System.Globalization;
using System.IO;
using System.Text;

namespace MemoryOptimizer;

internal static class OptimizationHistoryLogger
{
    public static void Append(
        DateTime timestamp,
        string mode,
        ulong beforeAvailable,
        ulong afterAvailable,
        bool succeeded,
        string message)
    {
        AppDataPaths.EnsureRoot();
        var exists = File.Exists(AppDataPaths.HistoryPath);
        using var writer = new StreamWriter(AppDataPaths.HistoryPath, append: true, Encoding.UTF8);

        if (!exists)
            writer.WriteLine("timestamp,mode,before_available,after_available,freed,succeeded,message");

        var freed = afterAvailable > beforeAvailable ? afterAvailable - beforeAvailable : 0;
        writer.WriteLine(string.Join(
            ',',
            Escape(timestamp.ToString("O", CultureInfo.InvariantCulture)),
            Escape(mode),
            beforeAvailable.ToString(CultureInfo.InvariantCulture),
            afterAvailable.ToString(CultureInfo.InvariantCulture),
            freed.ToString(CultureInfo.InvariantCulture),
            succeeded ? "true" : "false",
            Escape(message)));
    }

    private static string Escape(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return '"' + value.Replace("\"", "\"\"") + '"';
    }
}
