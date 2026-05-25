using System.Windows;

namespace MemoryOptimizer;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings.Clone();

        DefaultScopeBox.Items.Add(new ScopeItem("推荐优化", MemoryOptimizationScope.Recommended));
        DefaultScopeBox.Items.Add(new ScopeItem("深度优化", MemoryOptimizationScope.All));
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

    private sealed record ScopeItem(string Name, MemoryOptimizationScope Scope);
}
