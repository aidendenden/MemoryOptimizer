

# Memory Optimizer v1.0.2

<div align="center">
  <img src="./Assets/AppIcon.png" width="200" alt="Memory Optimizer">
</div>

Author: aidendenden  
License: MIT

Memory Optimizer is a Windows desktop utility for viewing physical memory status and running controlled system-level memory optimization. It includes light/deep optimization modes, before/after charts, system tray support, history logs, and bilingual UI.

## Features

- Memory dashboard for load, available memory, and total memory.
- Light optimization for conservative cleanup.
- Deep optimization for more aggressive cleanup such as file cache handling.
- Chinese/English UI toggle.
- Before/after memory chart.
- Settings for auto refresh and automatic elevation.
- System tray support.
- CSV history log at `%APPDATA%\MemoryOptimizer\optimization-history.csv`.
- Author, MIT license, and v1.0.2 displayed in the UI.

## Working Principle

This tool does not "create" memory, nor does it fix memory leaks within applications. It primarily uses:

- `GlobalMemoryStatusEx` to read the current physical memory status.
- `NtSetSystemInformation` to trigger Windows system memory list operations.
- `RtlAdjustPrivilege` to acquire necessary memory management privileges.

The `optimize` command requires administrator privileges. Deep optimization may clear file caches, which could cause brief stuttering on HDDs or in low memory pressure scenarios.

## Usage

Download `MemoryOptimizer-win-x64.zip` from the Releases, extract it, and run. The release package is a framework-dependent single-file build, requiring the .NET 7 Desktop Runtime to be installed on the system:

```text
MemoryOptimizer.exe
```

Command-line usage is also available:

```powershell
MemoryOptimizer.exe status
MemoryOptimizer.exe optimize --light
MemoryOptimizer.exe optimize --full
MemoryOptimizer.exe optimize --dry-run
```

## Build

```powershell
cd E:\fork\MemoryOptimizer
dotnet publish -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o E:\fork\MemoryOptimizer\dist\MemoryOptimizer-Standalone
```

## Release Notes

### v1.0.2

- Reduced steady-state working set below 100 MB by trimming the app process after idle refreshes.
- Deferred system tray initialization until the window is minimized.
- Switched the Windows release package to framework-dependent single-file publishing to avoid bundling the full .NET desktop runtime.

### v1.0.1

- Removed the ambiguous default optimization button, tray action, and settings option.
- Renamed recommended optimization to light optimization.
- Updated package metadata and UI version display.

### V1.0.0

- First stable release.
- Added Chinese/English UI switching.
- Added custom app icon.
- Added light/deep optimization modes.
- Added memory chart, history log, settings window, tray support, and MIT license metadata.
