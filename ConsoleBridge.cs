using System.IO;
using System.Text;

namespace MemoryOptimizer;

internal static class ConsoleBridge
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

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
            Console.SetOut(new StreamWriter(output, Utf8NoBom) { AutoFlush = true });
            Console.SetError(new StreamWriter(error, Utf8NoBom) { AutoFlush = true });
        }
        catch
        {
            // Keep the default writers when the process was launched without a parent console.
        }
    }
}
