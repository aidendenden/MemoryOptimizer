using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using MemoryOptimizer;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("This tool only supports Windows.");
    return 1;
}

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "status";

try
{
    return command switch
    {
        "status" => ShowStatus(),
        "optimize" => Optimize(args.Skip(1).ToArray()),
        "trim" => TrimProcesses(args.Skip(1).ToArray()),
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

static int ShowStatus()
{
    var status = MemoryStatus.Query();
    Console.WriteLine($"Memory load: {status.LoadPercent:0}%");
    Console.WriteLine($"Available:   {ByteSize.Format(status.AvailablePhysicalBytes)}");
    Console.WriteLine($"Total:       {ByteSize.Format(status.TotalPhysicalBytes)}");
    Console.WriteLine($"Admin:       {(WindowsSecurity.IsAdministrator() ? "yes" : "no")}");
    return 0;
}

static int Optimize(string[] args)
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

static int TrimProcesses(string[] args)
{
    var processName = TryReadOption(args, "--name");
    var dryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));

    if (args.Any(a => a is "--help" or "-h"))
    {
        Console.WriteLine("Usage: MemoryOptimizer trim [--name processName] [--dry-run]");
        return 0;
    }

    var result = ProcessWorkingSetTrimmer.Trim(processName, dryRun);
    Console.WriteLine($"Matched: {result.Matched}");
    Console.WriteLine($"Trimmed: {result.Trimmed}");
    Console.WriteLine($"Failed:  {result.Failed}");
    if (dryRun) Console.WriteLine("Dry run: no process working set was trimmed.");
    return result.Failed == 0 ? 0 : 3;
}

static string? TryReadOption(string[] args, string option)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];

        var prefix = option + "=";
        if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return args[i][prefix.Length..];
    }

    return null;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    ShowHelp();
    return 1;
}

static int ShowHelp()
{
    Console.WriteLine("""
    Usage:
      MemoryOptimizer status
      MemoryOptimizer optimize [--recommended|--full|--scope <names>] [--no-elevate] [--dry-run]
      MemoryOptimizer trim [--name processName] [--dry-run]

    Commands:
      status     Show current physical memory status.
      optimize   Run PCL-style system memory optimization. Requires administrator privileges.
      trim       Trim process working sets with EmptyWorkingSet. This is less powerful than optimize.
    """);
    return 0;
}

static void ShowOptimizeHelp()
{
    Console.WriteLine("""
    Usage:
      MemoryOptimizer optimize [--recommended|--full|--scope <names>] [--no-elevate] [--dry-run]

    Scopes:
      EmptyWorkingSets
      FlushFileCache
      FlushModifiedList
      PurgeStandbyList
      PurgeLowPriorityStandbyList
      RegistryReconciliation
      CombinePhysicalMemory

    Examples:
      MemoryOptimizer optimize --recommended
      MemoryOptimizer optimize --full
      MemoryOptimizer optimize --scope EmptyWorkingSets,PurgeStandbyList
    """);
}
