using System.ComponentModel;
using System.Windows;

namespace MemoryOptimizer;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            ConsoleBridge.AttachForCli();
            Console.Error.WriteLine("This tool only supports Windows.");
            return 1;
        }

        var command = args.FirstOrDefault()?.ToLowerInvariant();
        if (command is null or "" or "gui" or "--gui")
            return RunGui(command is "gui" or "--gui" ? args.Skip(1).ToArray() : args);

        ConsoleBridge.AttachForCli();

        try
        {
            return command switch
            {
                "status" => ShowStatus(),
                "optimize" => Optimize(args.Skip(1).ToArray()),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => Unknown(command)
            };
        }
        catch (Win32Exception ex)
        {
            Console.Error.WriteLine($"Win32 error {ex.NativeErrorCode}: {ex.Message}");
            return ex.NativeErrorCode == 1223 ? 2 : 10;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 10;
        }
    }

    private static int RunGui(string[] args)
    {
        var app = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };

        var window = new MainWindow(ReadStartupScope(args));
        return app.Run(window);
    }

    private static MemoryOptimizationScope? ReadStartupScope(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--run", StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length)
                continue;

            return args[i + 1].ToLowerInvariant() switch
            {
                "light" or "recommended" => MemoryOptimizationScope.Light,
                "full" => MemoryOptimizationScope.All,
                _ => null
            };
        }

        return null;
    }

    private static int ShowStatus()
    {
        var status = MemoryStatus.Query();
        Console.WriteLine($"Memory load: {status.LoadPercent:0}%");
        Console.WriteLine($"Available:   {ByteSize.Format(status.AvailablePhysicalBytes)}");
        Console.WriteLine($"Total:       {ByteSize.Format(status.TotalPhysicalBytes)}");
        Console.WriteLine($"Admin:       {(WindowsSecurity.IsAdministrator() ? "yes" : "no")}");
        return 0;
    }

    private static int Optimize(string[] args)
    {
        var options = OptimizeOptions.Parse(args);
        if (options.ShowHelp)
        {
            ShowOptimizeHelp();
            return 0;
        }

        var scope = options.Scope;
        Console.WriteLine($"Scope: {scope}");

        if (options.DryRun)
        {
            Console.WriteLine("Dry run: no memory operation was executed.");
            return 0;
        }

        if (!WindowsSecurity.IsAdministrator())
        {
            if (options.NoElevate)
            {
                Console.Error.WriteLine("Full memory optimization requires administrator privileges.");
                return 5;
            }

            Console.WriteLine("Requesting administrator privileges...");
            return WindowsSecurity.RelaunchElevated(Environment.GetCommandLineArgs().Skip(1));
        }

        var before = MemoryStatus.Query();
        Console.WriteLine($"Before: {ByteSize.Format(before.AvailablePhysicalBytes)} available");

        MemoryOptimizerEngine.Optimize(scope);

        var after = MemoryStatus.Query();
        var diff = Math.Max(0, after.AvailablePhysicalBytes - before.AvailablePhysicalBytes);
        Console.WriteLine($"After:  {ByteSize.Format(after.AvailablePhysicalBytes)} available");
        Console.WriteLine($"Freed:  {ByteSize.Format(diff)}");
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        ShowHelp();
        return 1;
    }

    private static int ShowHelp()
    {
        Console.WriteLine("""
        Usage:
          MemoryOptimizer
          MemoryOptimizer status
          MemoryOptimizer optimize [--light|--full|--scope <names>] [--no-elevate] [--dry-run]

        Commands:
          no args    Open the desktop window.
          status     Show current physical memory status.
          optimize   Run PCL-style system memory optimization. Requires administrator privileges.
        """);
        return 0;
    }

    private static void ShowOptimizeHelp()
    {
        Console.WriteLine("""
        Usage:
          MemoryOptimizer optimize [--light|--full|--scope <names>] [--no-elevate] [--dry-run]

        Scopes:
          EmptyWorkingSets
          FlushFileCache
          FlushModifiedList
          PurgeStandbyList
          PurgeLowPriorityStandbyList
          RegistryReconciliation
          CombinePhysicalMemory

        Examples:
          MemoryOptimizer optimize --light
          MemoryOptimizer optimize --full
          MemoryOptimizer optimize --scope EmptyWorkingSets,PurgeStandbyList
        """);
    }
}
