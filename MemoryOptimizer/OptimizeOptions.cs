namespace MemoryOptimizer;

internal sealed record OptimizeOptions
{
    public MemoryOptimizationScope Scope { get; init; } = MemoryOptimizationScope.Recommended;
    public bool NoElevate { get; init; }
    public bool DryRun { get; init; }
    public bool ShowHelp { get; init; }

    public static OptimizeOptions Parse(string[] args)
    {
        var options = new OptimizeOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "--help" or "-h")
            {
                options = options with { ShowHelp = true };
            }
            else if (arg.Equals("--recommended", StringComparison.OrdinalIgnoreCase))
            {
                options = options with { Scope = MemoryOptimizationScope.Recommended };
            }
            else if (arg.Equals("--full", StringComparison.OrdinalIgnoreCase))
            {
                options = options with { Scope = MemoryOptimizationScope.All };
            }
            else if (arg.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase))
            {
                options = options with { NoElevate = true };
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options = options with { DryRun = true };
            }
            else if (arg.Equals("--scope", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options = options with { Scope = ParseScope(args[++i]) };
            }
            else if (arg.StartsWith("--scope=", StringComparison.OrdinalIgnoreCase))
            {
                options = options with { Scope = ParseScope(arg["--scope=".Length..]) };
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
