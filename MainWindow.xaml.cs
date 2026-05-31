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
    private readonly DispatcherTimer _memoryTrimTimer = new(DispatcherPriority.ApplicationIdle);
    private readonly bool _isAdministrator = WindowsSecurity.IsAdministrator();
    private readonly List<MemoryChartPoint> _chartPoints = new(capacity: 32);
    private CancellationTokenSource? _operationCancellation;
    private WinForms.NotifyIcon? _trayIcon;
    private string _lastChartSummary = "";
    private bool _closeAfterCancellation;
    private bool _isBusy;

    public MainWindow(MemoryOptimizationScope? startupScope = null)
    {
        InitializeComponent();
        _startupScope = startupScope;

        _lastChartSummary = T("NoChartHistory");
        ApplySettings();
        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _memoryTrimTimer.Interval = TimeSpan.FromMilliseconds(1500);
        _memoryTrimTimer.Tick += (_, _) =>
        {
            _memoryTrimTimer.Stop();
            ProcessMemoryTrimmer.TrimWorkingSet();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        AddChartPoint(MemoryStatus.Query(), T("StartupLabel"));
        AppendLog(T("Started"));

        if (_startupScope.HasValue)
            await RunOptimizationAsync(_startupScope.Value, GetScopeLabel(_startupScope.Value));
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();

    private async void Light_Click(object sender, RoutedEventArgs e) =>
        await RequestOptimizationAsync(MemoryOptimizationScope.Light, GetScopeLabel(MemoryOptimizationScope.Light));

    private async void Full_Click(object sender, RoutedEventArgs e) =>
        await RequestOptimizationAsync(MemoryOptimizationScope.All, GetScopeLabel(MemoryOptimizationScope.All));

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        RequestCancel(T("CancelingState"), T("CancelingLog"));
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isBusy || _operationCancellation is null) return;

        e.Cancel = true;
        _closeAfterCancellation = true;
        RequestCancel(T("ClosingState"), T("ClosingLog"));
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _refreshTimer.Stop();
        _memoryTrimTimer.Stop();
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;

        ConfigureTrayIcon();
        Hide();
        _trayIcon?.ShowBalloonTip(1500, "Memory Optimizer", T("TrayMinimized"), WinForms.ToolTipIcon.Info);
        RequestMemoryTrim();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings, _settings.Language)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() != true) return;

        _settings.CopyFrom(settingsWindow.Settings);
        _settings.Save();
        ApplySettings();
        if (_trayIcon is not null)
            ConfigureTrayIcon();
        RefreshStatus();
        DrawMemoryChart();
        AppendLog(T("SettingsSaved"));
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        _settings.Language = _settings.Language == UiLanguage.Chinese ? UiLanguage.English : UiLanguage.Chinese;
        _settings.Save();
        ApplySettings();
        if (_trayIcon is not null)
            ConfigureTrayIcon();
        RefreshStatus();
        DrawMemoryChart();
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
                StateText.Text = T("ElevationDisabledState");
                AppendLog(T("ElevationDisabledLog"));
                return;
            }

            var scopeArg = scope == MemoryOptimizationScope.All ? "full" : "light";
            AppendLog(string.Format(T("OpeningElevated"), label));
            var result = WindowsSecurity.RelaunchElevated(new[] { "gui", "--run", scopeArg });
            if (result == 0)
            {
                AppendLog(T("ElevatedStarted"));
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                StateText.Text = T("ElevatedFailed");
                AppendLog(T("ElevatedFailed"));
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
        SetBusy(true, string.Format(T("Running"), label));
        try
        {
            AddChartPoint(before, $"{label} {T("BeforeSuffix")}");
            AppendLog(string.Format(T("OptimizeStart"), label, ByteSize.Format(before.AvailablePhysicalBytes)));

            after = await Task.Run(() =>
            {
                MemoryOptimizerEngine.Optimize(
                    scope,
                    cancellation.Token,
                    step => _ = Dispatcher.BeginInvoke(new Action(() => AppendLog(string.Format(T("StepProgress"), LocalizeStep(step))))));

                cancellation.Token.ThrowIfCancellationRequested();
                return MemoryStatus.Query();
            }, cancellation.Token);

            var diff = Math.Max(0, after.AvailablePhysicalBytes - before.AvailablePhysicalBytes);
            AddChartPoint(after, $"{label} {T("AfterSuffix")}");
            _lastChartSummary = string.Format(
                T("ChartSummary"),
                label,
                ByteSize.Format(before.AvailablePhysicalBytes),
                ByteSize.Format(after.AvailablePhysicalBytes),
                ByteSize.Format(diff));
            AppendLog(string.Format(T("OptimizeDoneLog"), label, ByteSize.Format(after.AvailablePhysicalBytes), ByteSize.Format(diff)));
            StateText.Text = string.Format(T("OptimizeDoneState"), label, ByteSize.Format(diff));
            OptimizationHistoryLogger.Append(DateTime.Now, label, before.AvailablePhysicalBytes, after.AvailablePhysicalBytes, true, T("HistoryComplete"));
        }
        catch (OperationCanceledException)
        {
            AppendLog(string.Format(T("OptimizeCanceled"), label));
            StateText.Text = T("CanceledState");
            OptimizationHistoryLogger.Append(DateTime.Now, label, before.AvailablePhysicalBytes, after.AvailablePhysicalBytes, false, T("HistoryCanceled"));
        }
        catch (Exception ex)
        {
            AppendLog(string.Format(T("OptimizeFailed"), label, ex.Message));
            StateText.Text = string.Format(T("FailedState"), label);
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
        AdminText.Text = _isAdministrator ? T("AdminYes") : T("AdminNo");
        FooterText.Text = string.Format(T("LastRefresh"), DateTime.Now);
        CompatibilityText.Text = string.Format(
            T("Compatibility"),
            Environment.OSVersion.Version,
            _isAdministrator ? T("AdminReady") : T("AdminElevationNeeded"));

        if (!_isBusy)
            StateText.Text = _isAdministrator
                ? T("DirectOptimizeReady")
                : T("ElevationRequiredState");

        if (_settings.AutoRefresh)
            AddChartPoint(status, T("RefreshLabel"));

        if (!_isBusy)
            RequestMemoryTrim();
    }

    private void SetBusy(bool value, string? stateText = null)
    {
        _isBusy = value;
        BusyBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !value;
        LightButton.IsEnabled = !value;
        FullButton.IsEnabled = !value;
        SettingsButton.IsEnabled = !value;
        LanguageButton.IsEnabled = !value;
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
        ApplyLanguage();
        _refreshTimer.Interval = TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds);
        if (_settings.AutoRefresh)
            _refreshTimer.Start();
        else
            _refreshTimer.Stop();
    }

    private void RequestMemoryTrim()
    {
        if (_isBusy) return;

        _memoryTrimTimer.Stop();
        _memoryTrimTimer.Start();
    }

    private void ApplyLanguage()
    {
        Title = T("AppTitle");
        TitleText.Text = T("AppTitle");
        SubtitleText.Text = T("AppSubtitle");
        MemoryLoadLabel.Text = T("MemoryLoad");
        AvailableLabel.Text = T("AvailableMemory");
        TotalLabel.Text = T("TotalMemory");
        StateLabel.Text = T("Status");
        RefreshButton.Content = T("Refresh");
        LightButton.Content = T("LightOptimize");
        FullButton.Content = T("FullOptimize");
        SettingsButton.Content = T("Settings");
        LanguageButton.Content = T("LanguageToggle");
        CancelButton.Content = T("Cancel");
        RiskTitleText.Text = T("RiskTitle");
        RiskBodyText.Text = T("RiskBody");
        CreditText.Text = T("FooterCredit");

        if (string.IsNullOrWhiteSpace(_lastChartSummary))
            _lastChartSummary = T("NoChartHistory");
    }

    private void ConfigureTrayIcon()
    {
        _trayIcon?.Dispose();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(T("TrayShow"), null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add(T("TrayExit"), null, (_, _) => Dispatcher.Invoke(RequestExit));

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
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"));
            if (resource?.Stream is not null)
            {
                using var source = resource.Stream;
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                buffer.Position = 0;

                using var icon = new Icon(buffer);
                return (Icon)icon.Clone();
            }
        }
        catch
        {
            // Fall back to the Windows default icon if the embedded resource cannot be loaded.
        }

        return SystemIcons.Application;
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
        ChartText.Text = string.Format(T("RecentChart"), _lastChartSummary, last.Label, ByteSize.Format(last.AvailableBytes));
    }

    private string GetScopeLabel(MemoryOptimizationScope scope) =>
        scope == MemoryOptimizationScope.All ? T("FullOptimize") : T("LightOptimize");

    private string T(string key) => TextCatalog.T(_settings.Language, key);

    private string LocalizeStep(string step) => _settings.Language == UiLanguage.Chinese
        ? step
        : step switch
        {
            "获取系统内存管理权限" => "acquiring memory-management privileges",
            "清理进程工作集" => "emptying process working sets",
            "刷新文件缓存" => "flushing file cache",
            "刷新已修改页面列表" => "flushing modified page list",
            "清理备用页面列表" => "purging standby list",
            "清理低优先级备用页面" => "purging low-priority standby list",
            "同步注册表内存" => "reconciling registry memory",
            "合并物理内存页面" => "combining physical memory pages",
            _ => step
        };

    private readonly record struct MemoryChartPoint(DateTime Time, string Label, ulong AvailableBytes, ulong TotalBytes);
}
