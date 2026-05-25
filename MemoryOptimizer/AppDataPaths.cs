using System.IO;

namespace MemoryOptimizer;

internal static class AppDataPaths
{
    private static readonly string RootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MemoryOptimizer");

    public static string SettingsPath => Path.Combine(RootPath, "settings.json");
    public static string HistoryPath => Path.Combine(RootPath, "optimization-history.csv");

    public static void EnsureRoot() => Directory.CreateDirectory(RootPath);
}
