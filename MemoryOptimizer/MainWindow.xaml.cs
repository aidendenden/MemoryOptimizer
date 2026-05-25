using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace MemoryOptimizer;

public partial class MainWindow : Window
{
    private readonly MemoryOptimizationScope? _startupScope;
    private readonly DispatcherTimer _refreshTimer = new();
    private CancellationTokenSource? _operationCancellation;
    private bool _closeAfterCancellation;
    private bool _isBusy;

    public MainWindow(MemoryOptimizationScope? startupScope = null)
    {
        InitializeComponent();
        _startupScope = startupScope;

        _refreshTimer.Interval = TimeSpan.FromSeconds(5);
        _refreshTimer.Tick += (_, _) => RefreshStatus();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        _refreshTimer.Start();
        AppendLog("程序已启动。");

        if (_startupScope.HasValue)
            await RunOptimizationAsync(_startupScope.Value, GetScopeLabel(_startupScope.Value));
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();

    private async void Recommended_Click(object sender, RoutedEventArgs e) =>
        await RequestOptimizationAsync(MemoryOptimizationScope.Recommended, "推荐优化");

    private async void Full_Click(object sender, RoutedEventArgs e) =>
        await RequestOptimizationAsync(MemoryOptimizationScope.All, "深度优化");

    private async void TrimJava_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        SetBusy(true, "正在裁剪 Java 进程工作集...");
        try
        {
            var result = await Task.Run(
                () => ProcessWorkingSetTrimmer.Trim("java", dryRun: false, cancellation.Token),
                cancellation.Token);
            AppendLog($"Java 工作集裁剪完成。匹配 {result.Matched}，成功 {result.Trimmed}，失败 {result.Failed}。");
            StateText.Text = result.Matched == 0 ? "未找到 Java 进程。" : "Java 工作集裁剪完成。";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Java 工作集裁剪已取消。");
            StateText.Text = "已取消。";
        }
        catch (Exception ex)
        {
            AppendLog($"裁剪 Java 失败：{ex.Message}");
            StateText.Text = "裁剪 Java 失败。";
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

        if (!WindowsSecurity.IsAdministrator())
        {
            var scopeArg = scope == MemoryOptimizationScope.All ? "full" : "recommended";
            AppendLog($"{label} 需要管理员权限，正在打开管理员窗口...");
            var result = WindowsSecurity.RelaunchElevated(new[] { "gui", "--run", scopeArg });
            if (result == 0)
            {
                AppendLog("管理员窗口已启动，正在关闭当前普通权限窗口。");
                Application.Current.Shutdown();
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
        _operationCancellation = cancellation;
        SetBusy(true, $"正在执行{label}...");
        try
        {
            var before = MemoryStatus.Query();
            AppendLog($"{label}开始。优化前可用内存：{ByteSize.Format(before.AvailablePhysicalBytes)}。");

            var after = await Task.Run(() =>
            {
                MemoryOptimizerEngine.Optimize(
                    scope,
                    cancellation.Token,
                    step => Dispatcher.Invoke(() => AppendLog($"正在{step}...")));

                cancellation.Token.ThrowIfCancellationRequested();
                return MemoryStatus.Query();
            }, cancellation.Token);

            var diff = Math.Max(0, after.AvailablePhysicalBytes - before.AvailablePhysicalBytes);
            AppendLog($"{label}完成。当前可用内存：{ByteSize.Format(after.AvailablePhysicalBytes)}，释放约 {ByteSize.Format(diff)}。");
            StateText.Text = $"{label}完成，释放约 {ByteSize.Format(diff)}。";
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{label}已取消。");
            StateText.Text = "已取消。";
        }
        catch (Exception ex)
        {
            AppendLog($"{label}失败：{ex.Message}");
            StateText.Text = $"{label}失败。";
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
        AdminText.Text = WindowsSecurity.IsAdministrator() ? "管理员：是" : "管理员：否";
        FooterText.Text = $"最后刷新：{DateTime.Now:HH:mm:ss}";

        if (!_isBusy)
            StateText.Text = WindowsSecurity.IsAdministrator()
                ? "可直接执行系统级优化。"
                : "系统级优化会请求管理员权限。";
    }

    private void SetBusy(bool value, string? stateText = null)
    {
        _isBusy = value;
        BusyBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !value;
        RecommendedButton.IsEnabled = !value;
        FullButton.IsEnabled = !value;
        TrimJavaButton.IsEnabled = !value;
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

    private static string GetScopeLabel(MemoryOptimizationScope scope) =>
        scope == MemoryOptimizationScope.All ? "深度优化" : "推荐优化";
}
