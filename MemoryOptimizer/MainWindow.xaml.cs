using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace MemoryOptimizer;

public partial class MainWindow : Window
{
    private readonly MemoryOptimizationScope? _startupScope;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly bool _isAdministrator = WindowsSecurity.IsAdministrator();
    private readonly List<MemoryChartPoint> _chartPoints = new(capacity: 32);
    private CancellationTokenSource? _operationCancellation;
    private WinForms.NotifyIcon? _trayIcon;
    private string _lastChartSummary = "暂无优化记录。";
    private bool _closeAfterCancellation;
    private bool _isBusy;

    public MainWindow(MemoryOptimizationScope? startupScope = null)
    {
        InitializeComponent();
        _startupScope = startupScope;

        ApplySettings();
        ConfigureTrayIcon();
        _refreshTimer.Tick += (_, _) => RefreshStatus();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        AddChartPoint(MemoryStatus.Query(), "启动");
        AppendLog("程序已启动。");

        if (_startupScope.HasValue)
            await RunOptimizationAsync(_startupScope.Value, GetScopeLabel(_startupScope.Value));
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();

    private async void Default_Click(object sender, RoutedEventArgs e) =>
        await RequestOptimizationAsync(_settings.DefaultScope, $"{GetScopeLabel(_settings.DefaultScope)}（默认）");

    private async void Recommended_Click(object sender, RoutedEventArgs e) =>
        await RequestOptimizationAsync(MemoryOptimizationScope.Recommended, "推荐优化");

    private async void Full_Click(object sender, RoutedEventArgs e) =>
        await RequestOptimizationAsync(MemoryOptimizationScope.All, "深度优化");

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        RequestCancel("正在取消，当前步骤结束后停止。", "已请求取消，当前步骤结束后会停止后续操作。");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isBusy || _operationCancellation is null) return;

        e.Cancel = true;
        _closeAfterCancellation = true;
        RequestCancel("正在关闭，当前步骤结束后退出。", "已请求关闭，当前步骤结束后会退出。");
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;

        Hide();
        _trayIcon?.ShowBalloonTip(1500, "Memory Optimizer", "已最小化到系统托盘。", WinForms.ToolTipIcon.Info);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() != true) return;

        _settings.CopyFrom(settingsWindow.Settings);
        _settings.Save();
        ApplySettings();
        RefreshStatus();
        AppendLog("设置已保存。");
    }

    private void RequestCancel(string stateText, string logMessage)
    {
        if (_operationCancellation is null || _operationCancellation.IsCancellationRequested) return;

        _operationCancellation.Cancel();
        CancelButton.IsEnabled = false;
        StateText.Text = stateText;
        AppendLog(logMessage);
    }

    private async Task RequestOptimizationAsync(MemoryOptimizationScope scope, string label)
    {
        if (_isBusy) return;

        if (!_isAdministrator)
        {
            if (!_settings.AutoRequestElevation)
            {
                StateText.Text = "当前不是管理员权限，设置已关闭自动提权。";
                AppendLog("当前不是管理员权限，已按设置取消自动提权。");
                return;
            }

            var scopeArg = scope == MemoryOptimizationScope.All ? "full" : "recommended";
            AppendLog($"{label} 需要管理员权限，正在打开管理员窗口...");
            var result = WindowsSecurity.RelaunchElevated(new[] { "gui", "--run", scopeArg });
            if (result == 0)
            {
                AppendLog("管理员窗口已启动，正在关闭当前普通权限窗口。");
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                StateText.Text = "管理员窗口未启动，优化已取消。";
                AppendLog("管理员窗口未启动，优化已取消。");
            }
            return;
        }

        await RunOptimizationAsync(scope, label);
    }

    private async Task RunOptimizationAsync(MemoryOptimizationScope scope, string label)
    {
        if (_isBusy) return;

        using var cancellation = new CancellationTokenSource();
        var before = MemoryStatus.Query();
        var after = before;
        _operationCancellation = cancellation;
        SetBusy(true, $"正在执行{label}...");
        try
        {
            AddChartPoint(before, $"{label}前");
            AppendLog($"{label}开始。优化前可用内存：{ByteSize.Format(before.AvailablePhysicalBytes)}。");

            after = await Task.Run(() =>
            {
                MemoryOptimizerEngine.Optimize(
                    scope,
                    cancellation.Token,
                    step => _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"正在{step}..."))));

                cancellation.Token.ThrowIfCancellationRequested();
                return MemoryStatus.Query();
            }, cancellation.Token);

            var diff = Math.Max(0, after.AvailablePhysicalBytes - before.AvailablePhysicalBytes);
            AddChartPoint(after, $"{label}后");
            _lastChartSummary = $"{label}: {ByteSize.Format(before.AvailablePhysicalBytes)} -> {ByteSize.Format(after.AvailablePhysicalBytes)}，释放约 {ByteSize.Format(diff)}。";
            AppendLog($"{label}完成。当前可用内存：{ByteSize.Format(after.AvailablePhysicalBytes)}，释放约 {ByteSize.Format(diff)}。");
            StateText.Text = $"{label}完成，释放约 {ByteSize.Format(diff)}。";
            OptimizationHistoryLogger.Append(DateTime.Now, label, before.AvailablePhysicalBytes, after.AvailablePhysicalBytes, true, "完成");
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{label}已取消。");
            StateText.Text = "已取消。";
            OptimizationHistoryLogger.Append(DateTime.Now, label, before.AvailablePhysicalBytes, after.AvailablePhysicalBytes, false, "取消");
        }
        catch (Exception ex)
        {
            AppendLog($"{label}失败：{ex.Message}");
            StateText.Text = $"{label}失败。";
            OptimizationHistoryLogger.Append(DateTime.Now, label, before.AvailablePhysicalBytes, after.AvailablePhysicalBytes, false, ex.Message);
        }
        finally
        {
            _operationCancellation = null;
            SetBusy(false);
            if (_closeAfterCancellation)
            {
                _ = Dispatcher.BeginInvoke(Close);
            }
            else
            {
                RefreshStatus();
            }
        }
    }

    private void RefreshStatus()
    {
        var status = MemoryStatus.Query();
        MemoryLoadText.Text = $"{status.LoadPercent:0}%";
        MemoryBar.Value = status.LoadPercent;
        AvailableText.Text = ByteSize.Format(status.AvailablePhysicalBytes);
        TotalText.Text = ByteSize.Format(status.TotalPhysicalBytes);
        AdminText.Text = _isAdministrator ? "管理员：是" : "管理员：否";
        FooterText.Text = $"最后刷新：{DateTime.Now:HH:mm:ss}";
        CompatibilityText.Text = $"兼容性：Windows {Environment.OSVersion.Version}，{(_isAdministrator ? "当前已具备管理员权限" : "系统级优化需要 UAC 提权")}。";

        if (!_isBusy)
            StateText.Text = _isAdministrator
                ? "可直接执行系统级优化。"
                : "系统级优化会请求管理员权限。";

        if (_settings.AutoRefresh)
            AddChartPoint(status, "刷新");
    }

    private void SetBusy(bool value, string? stateText = null)
    {
        _isBusy = value;
        BusyBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !value;
        DefaultButton.IsEnabled = !value;
        RecommendedButton.IsEnabled = !value;
        FullButton.IsEnabled = !value;
        SettingsButton.IsEnabled = !value;
        CancelButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = value;

        if (!string.IsNullOrWhiteSpace(stateText))
            StateText.Text = stateText;
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private void ApplySettings()
    {
        _refreshTimer.Interval = TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds);
        if (_settings.AutoRefresh)
            _refreshTimer.Start();
        else
            _refreshTimer.Stop();

        DefaultButton.Content = $"默认优化：{GetScopeLabel(_settings.DefaultScope)}";
    }

    private void ConfigureTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("显示窗口", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("执行默认优化", null, async (_, _) =>
            await Dispatcher.InvokeAsync(async () => await RequestOptimizationAsync(_settings.DefaultScope, $"{GetScopeLabel(_settings.DefaultScope)}（托盘）")));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(RequestExit));

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Memory Optimizer",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private static Icon LoadTrayIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RequestExit()
    {
        Close();
    }

    private void HistoryChart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawMemoryChart();

    private void AddChartPoint(MemoryStatus status, string label)
    {
        if (_chartPoints.Count == 32)
            _chartPoints.RemoveAt(0);

        _chartPoints.Add(new MemoryChartPoint(DateTime.Now, label, status.AvailablePhysicalBytes, status.TotalPhysicalBytes));
        DrawMemoryChart();
    }

    private void DrawMemoryChart()
    {
        if (HistoryChart is null) return;

        HistoryChart.Children.Clear();
        if (_chartPoints.Count == 0)
        {
            ChartText.Text = _lastChartSummary;
            return;
        }

        var width = Math.Max(HistoryChart.ActualWidth, 320);
        var height = Math.Max(HistoryChart.ActualHeight, 80);
        var total = Math.Max(1UL, _chartPoints[^1].TotalBytes);
        var points = new PointCollection(_chartPoints.Count);

        for (var i = 0; i < _chartPoints.Count; i++)
        {
            var x = _chartPoints.Count == 1 ? width : i * width / (_chartPoints.Count - 1);
            var ratio = Math.Clamp((double)_chartPoints[i].AvailableBytes / total, 0, 1);
            var y = height - ratio * (height - 8) - 4;
            points.Add(new System.Windows.Point(x, y));
        }

        HistoryChart.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 111, 237)),
            StrokeThickness = 2
        });

        foreach (var point in points)
        {
            HistoryChart.Children.Add(new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(47, 111, 237)),
                Margin = new Thickness(point.X - 2.5, point.Y - 2.5, 0, 0)
            });
        }

        var last = _chartPoints[^1];
        ChartText.Text = $"{_lastChartSummary} 最近记录：{last.Label}，可用 {ByteSize.Format(last.AvailableBytes)}。";
    }

    private static string GetScopeLabel(MemoryOptimizationScope scope) =>
        scope == MemoryOptimizationScope.All ? "深度优化" : "推荐优化";

    private readonly record struct MemoryChartPoint(DateTime Time, string Label, ulong AvailableBytes, ulong TotalBytes);
}
