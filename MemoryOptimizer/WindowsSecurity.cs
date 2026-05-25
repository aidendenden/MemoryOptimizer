using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace MemoryOptimizer;

internal static class WindowsSecurity
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static int RelaunchElevated(IEnumerable<string> originalArgs)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
            throw new InvalidOperationException("Unable to locate current executable.");

        var commandLine = Environment.GetCommandLineArgs();
        var userArgs = originalArgs.ToList();

        if (Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            && commandLine.Length > 0
            && commandLine[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            userArgs.Insert(0, commandLine[0]);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = string.Join(" ", userArgs.Select(QuoteArgument)),
            Verb = "runas",
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        try
        {
            using var process = Process.Start(startInfo);
            return process is null ? 6 : 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Console.Error.WriteLine("Administrator elevation was cancelled.");
            return 2;
        }
    }

    private static string QuoteArgument(string arg)
    {
        if (arg.Length == 0) return "\"\"";
        if (!arg.Any(char.IsWhiteSpace) && !arg.Contains('"')) return arg;

        var builder = new StringBuilder();
        builder.Append('"');

        var pendingBackslashes = 0;
        foreach (var character in arg)
        {
            switch (character)
            {
                case '\\':
                    pendingBackslashes++;
                    break;
                case '"':
                    builder.Append('\\', pendingBackslashes * 2 + 1);
                    builder.Append('"');
                    pendingBackslashes = 0;
                    break;
                default:
                    builder.Append('\\', pendingBackslashes);
                    builder.Append(character);
                    pendingBackslashes = 0;
                    break;
            }
        }

        builder.Append('\\', pendingBackslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
