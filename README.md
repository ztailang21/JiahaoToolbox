# 嘉豪工具箱（JiahaoToolbox）

一款面向 **Windows 10 / 11 (x64)** 的轻量级一体化系统工具箱，采用现代 Fluent 侧边栏与圆角卡片界面，单文件仅 ~180 KB，基于 Windows 内置 `.NET Framework 4.8` 与原生 Win32 API 构建，零外部依赖、双击即用。

## 核心模块

1. **总览**
   - 双卡片一眼查看后台效率监控与 Windows 防护/更新/智能应用控制的实时状态，一键直达对应管理页。

2. **效率管理（EcoQoS）**
   - 为选定桌面软件及其可信安装目录与实时父子进程树开启 Windows 效率模式（`ProcessPowerThrottling` + 可配置后台优先级）。
   - 支持 **前台自动豁免**（切回前台即时恢复原优先级与 EcoQoS，退回后台自动重新接管）。
   - 支持 **启用前预览受影响目录与进程**、**精准原状态快照与可逆还原**、**单应用进程详情面板**。

3. **防护与更新（按需一键提权）**
   - **Microsoft Defender**：实时状态与篡改保护检测、一键停用/启用、快捷打开安全中心、加入排除项。
   - **Windows 自动更新**：一键禁用/启用/全面修复更新服务，支持强化防恢复 ACL 锁定，并将 **「同时禁用微软商店安装服务（InstallService）」设为独立可选项（默认不勾选，保留商店正常使用）**。
   - **智能应用控制（Smart App Control · Win11）**：状态检测、关闭与实验性评估模式切换，自动兼容 Win10。

4. **通用设置与系统托盘**
   - 默认免管理员权限启动，支持 **免 UAC 静默托盘开机自启**（开启时自动清理旧版 AutoEcoControl 启动项）。
   - 配置文件统一保存在 `%APPDATA%\JiahaoToolbox\config.json`（首次启动自动继承旧版配置），放桌面运行零残留。
   - 需要在「防护与更新」页修改系统配置时，支持 **按需一键 UAC 提权并无缝回到当前页**。

## 本地编译

无需安装 Visual Studio 或 .NET SDK，直接运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产物输出至 `publish\JiahaoToolbox.exe`。
