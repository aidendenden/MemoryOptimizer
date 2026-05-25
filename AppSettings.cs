using System.IO;
using System.Text.Json;

namespace MemoryOptimizer;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MemoryOptimizationScope DefaultScope { get; set; } = MemoryOptimizationScope.Recommended;
    public UiLanguage Language { get; set; } = UiLanguage.Chinese;
    public bool AutoRefresh { get; set; } = true;
    public bool AutoRequestElevation { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 5;

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppDataPaths.SettingsPath)) return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppDataPaths.SettingsPath), JsonOptions)
                           ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public AppSettings Clone() => new()
    {
        DefaultScope = DefaultScope,
        Language = Language,
        AutoRefresh = AutoRefresh,
        AutoRequestElevation = AutoRequestElevation,
        RefreshIntervalSeconds = RefreshIntervalSeconds
    };

    public void CopyFrom(AppSettings source)
    {
        DefaultScope = source.DefaultScope;
        Language = source.Language;
        AutoRefresh = source.AutoRefresh;
        AutoRequestElevation = source.AutoRequestElevation;
        RefreshIntervalSeconds = source.RefreshIntervalSeconds;
        Normalize();
    }

    public void Save()
    {
        Normalize();
        AppDataPaths.EnsureRoot();
        File.WriteAllText(AppDataPaths.SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    private void Normalize()
    {
        if (DefaultScope is not (MemoryOptimizationScope.Recommended or MemoryOptimizationScope.All))
            DefaultScope = MemoryOptimizationScope.Recommended;

        if (Language is not (UiLanguage.Chinese or UiLanguage.English))
            Language = UiLanguage.Chinese;

        RefreshIntervalSeconds = Math.Clamp(RefreshIntervalSeconds, 2, 60);
    }
}
