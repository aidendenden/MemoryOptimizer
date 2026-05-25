namespace MemoryOptimizer;

internal sealed class OptimizeOptions
{
    public MemoryOptimizationScope Scope { get; private set; } = MemoryOptimizationScope.Light;
    public bool NoElevate { get; private set; }
    public bool DryRun { get; private set; }
    public bool ShowHelp { get; private set; }

    public static OptimizeOptions Parse(string[] args)
    {
        var options = new OptimizeOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "--help" or "-h")
            {
                options.ShowHelp = true;
            }
            else if (arg.Equals("--light", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--recommended", StringComparison.OrdinalIgnoreCase))
            {
                options.Scope = MemoryOptimizationScope.Light;
            }
            else if (arg.Equals("--full", StringComparison.OrdinalIgnoreCase))
            {
                options.Scope = MemoryOptimizationScope.All;
            }
            else if (arg.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase))
            {
                options.NoElevate = true;
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options.DryRun = true;
            }
            else if (arg.Equals("--scope", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.Scope = ParseScope(args[++i]);
            }
            else if (arg.StartsWith("--scope=", StringComparison.OrdinalIgnoreCase))
            {
                options.Scope = ParseScope(arg["--scope=".Length..]);
            }
            else
            {
                throw new ArgumentException($"Unknown optimize option: {arg}");
            }
        }

        return options;
    }

    private static MemoryOptimizationScope ParseScope(string value)
    {
        var scope = MemoryOptimizationScope.None;
        foreach (var item in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<MemoryOptimizationScope>(item, ignoreCase: true, out var parsed))
                throw new ArgumentException($"Unknown scope: {item}");

            scope |= parsed;
        }

        return scope;
    }
}
