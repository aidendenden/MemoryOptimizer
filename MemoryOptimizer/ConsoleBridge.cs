using System.IO;
using System.Text;

namespace MemoryOptimizer;

internal static class ConsoleBridge
{
    public static void AttachForCli()
    {
        if (!OperatingSystem.IsWindows()) return;

        NativeMethods.AttachParentConsole();
        ResetConsoleStreams();
    }

    private static void ResetConsoleStreams()
    {
        try
        {
            var output = Console.OpenStandardOutput();
            var error = Console.OpenStandardError();
            Console.SetOut(new StreamWriter(output, Encoding.UTF8) { AutoFlush = true });
            Console.SetError(new StreamWriter(error, Encoding.UTF8) { AutoFlush = true });
        }
        catch
        {
            // Keep the default writers when the process was launched without a parent console.
        }
    }
}
