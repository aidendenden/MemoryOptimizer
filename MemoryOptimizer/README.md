# MemoryOptimizer

一个 Windows 内存优化工具原型，目标是复刻 PCL2 / PCL CE 中“启动前优化内存”的核心思路。

默认启动会打开桌面窗口。窗口里可以查看当前物理内存状态，并执行推荐优化、深度优化，或取消正在排队的后续优化步骤。关闭窗口时如果仍在优化，会先请求取消，等当前步骤结束后再退出。

## 原理

这个工具不会“创造”内存，也不会修复应用本身的内存泄漏。它主要做两类事情：

- 读取当前物理内存状态：`GlobalMemoryStatusEx`
- 管理系统内存列表：`NtSetSystemInformation`

`optimize` 命令需要管理员权限，因为它会启用 `SeProfileSingleProcessPrivilege` 和 `SeIncreaseQuotaPrivilege` 后调用 NT 内核接口。机械硬盘环境下，清理备用列表或文件缓存后可能出现短时间卡顿，因为后续访问文件时需要重新从磁盘读取。

## 使用

```powershell
dotnet run --project .
dotnet run --project . -- status
dotnet run --project . -- optimize --dry-run
dotnet run --project . -- optimize --recommended
dotnet run --project . -- optimize --full
```

## 命令

- 无参数：打开桌面窗口。
- `status`：显示内存占用、可用物理内存、总物理内存和当前是否管理员。
- `optimize`：执行 PCL 风格的系统级优化，默认使用推荐范围；非管理员运行时会请求 UAC 提权。

## 优化范围

`--recommended` 包含：

- `EmptyWorkingSets`
- `FlushModifiedList`
- `PurgeStandbyList`
- `PurgeLowPriorityStandbyList`

`--full` 额外包含：

- `FlushFileCache`
- `RegistryReconciliation`
- `CombinePhysicalMemory`

也可以自定义：

```powershell
dotnet run --project . -- optimize --scope EmptyWorkingSets,PurgeStandbyList
```

## 构建

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```
