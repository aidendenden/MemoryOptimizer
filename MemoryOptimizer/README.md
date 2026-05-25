# Memory Optimizer V1.0.0

作者：aidendenden  
协议：MIT

Memory Optimizer 是一个面向 Windows 的桌面内存优化工具。它提供当前物理内存状态、推荐优化、深度优化、优化前后图表、系统托盘、历史日志和中英文 UI 切换。

Memory Optimizer is a Windows desktop utility for viewing physical memory status and running controlled system-level memory optimization. It includes recommended/deep optimization modes, before/after charts, tray support, history logging, and Chinese/English UI switching.

## 功能

- 内存状态看板：显示内存占用、可用内存和总内存。
- 推荐优化：执行相对保守的系统内存列表处理。
- 深度优化：额外处理文件缓存等更激进的优化项。
- 中英文切换：主窗口、设置窗口、按钮、状态和提示文本可切换。
- 优化图表：记录最近内存状态，展示可用内存变化。
- 设置窗口：默认优化模式、自动刷新、自动请求管理员权限。
- 系统托盘：最小化到托盘，可恢复窗口、执行默认优化或退出。
- 历史日志：写入 `%APPDATA%\MemoryOptimizer\optimization-history.csv`。
- 版本与授权信息：UI 底部显示作者、MIT 协议和 V1.0.0。

## Features

- Memory dashboard for load, available memory, and total memory.
- Recommended optimization for conservative cleanup.
- Deep optimization for more aggressive cleanup such as file cache handling.
- Chinese/English UI toggle.
- Before/after memory chart.
- Settings for default mode, auto refresh, and automatic elevation.
- System tray support.
- CSV history log at `%APPDATA%\MemoryOptimizer\optimization-history.csv`.
- Author, MIT license, and V1.0.0 displayed in the UI.

## 原理

这个工具不会“创造”内存，也不会修复应用自身的内存泄漏。它主要使用：

- `GlobalMemoryStatusEx` 读取当前物理内存状态。
- `NtSetSystemInformation` 触发 Windows 系统内存列表相关操作。
- `RtlAdjustPrivilege` 获取必要的内存管理权限。

`optimize` 需要管理员权限。深度优化可能清理文件缓存，机械硬盘或低内存压力场景下可能造成短时间卡顿。

## Usage

下载 Release 中的 `MemoryOptimizer-win-x64.zip`，解压后运行：

```text
MemoryOptimizer.exe
```

命令行也可用：

```powershell
MemoryOptimizer.exe status
MemoryOptimizer.exe optimize --recommended
MemoryOptimizer.exe optimize --full
MemoryOptimizer.exe optimize --dry-run
```

## Build

```powershell
cd E:\fork\Arkanoid\MemoryOptimizer
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o E:\fork\Arkanoid\dist\MemoryOptimizer-Standalone
```

## Release Notes

### V1.0.0

- First stable release.
- Added Chinese/English UI switching.
- Added custom app icon.
- Added recommended/deep optimization modes.
- Added memory chart, history log, settings window, tray support, and MIT license metadata.
