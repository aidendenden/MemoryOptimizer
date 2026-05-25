using System.Diagnostics;
using System.Runtime.Versioning;

namespace MemoryOptimizer;

internal readonly record struct TrimResult(int Matched, int Trimmed, int Failed);

internal static class ProcessWorkingSetTrimmer
{
    [SupportedOSPlatform("windows")]
    public static TrimResult Trim(
        string? processName,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var matched = 0;
        var trimmed = 0;
        var failed = 0;

        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (process)
            {
                try
                {
                    if (!Matches(process, processName)) continue;
                    matched++;

                    if (dryRun)
                    {
                        trimmed++;
                        continue;
                    }

                    if (NativeMethods.EmptyWorkingSet(process.Handle))
                        trimmed++;
                    else
                        failed++;
                }
                catch
                {
                    failed++;
                }
            }
        }

        return new TrimResult(matched, trimmed, failed);
    }

    private static bool Matches(Process process, string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return true;

        try
        {
            return process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
