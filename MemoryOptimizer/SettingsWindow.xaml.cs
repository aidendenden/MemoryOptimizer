using System.Windows;

namespace MemoryOptimizer;

public partial class SettingsWindow : Window
{
    private readonly UiLanguage _language;

    public AppSettings Settings { get; }

    public SettingsWindow(AppSettings settings, UiLanguage language)
    {
        InitializeComponent();
        _language = language;
        Settings = settings.Clone();

        ApplyLanguage();

        DefaultScopeBox.Items.Add(new ScopeItem(T("RecommendedOptimize"), MemoryOptimizationScope.Recommended));
        DefaultScopeBox.Items.Add(new ScopeItem(T("FullOptimize"), MemoryOptimizationScope.All));
        DefaultScopeBox.SelectedValuePath = nameof(ScopeItem.Scope);
        DefaultScopeBox.DisplayMemberPath = nameof(ScopeItem.Name);
        DefaultScopeBox.SelectedValue = Settings.DefaultScope;

        AutoRefreshBox.IsChecked = Settings.AutoRefresh;
        AutoElevationBox.IsChecked = Settings.AutoRequestElevation;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings.DefaultScope = DefaultScopeBox.SelectedValue is MemoryOptimizationScope scope
            ? scope
            : MemoryOptimizationScope.Recommended;
        Settings.AutoRefresh = AutoRefreshBox.IsChecked == true;
        Settings.AutoRequestElevation = AutoElevationBox.IsChecked == true;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ApplyLanguage()
    {
        Title = T("SettingsTitle");
        TitleText.Text = T("SettingsTitle");
        SubtitleText.Text = T("SettingsSubtitle");
        DefaultScopeLabel.Text = T("DefaultScope");
        AutoRefreshBox.Content = T("AutoRefresh");
        AutoElevationBox.Content = T("AutoElevation");
        RiskText.Text = T("SettingsRisk");
        CancelButton.Content = T("Cancel");
        SaveButton.Content = T("Save");
    }

    private string T(string key) => TextCatalog.T(_language, key);

    private sealed record ScopeItem(string Name, MemoryOptimizationScope Scope);
}
