using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace JiahaoToolbox.Core
{
    public static class WindowsUpdateManager
    {
        private const string AuPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
        private const string UxSettingsPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        private const string IfeoBasePath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\";
        private const string BenignStub = @"C:\Windows\System32\systray.exe";
        private const string StandardWuauservImagePath = @"%systemroot%\system32\svchost.exe -k netsvcs -p";

        private struct ServiceRule
        {
            public string Name;
            public int EnableStart;
            public int DisableStart;
            public bool LockWhenDisabled;
            public bool IsStoreService;

            public ServiceRule(string name, int enableStart, int disableStart, bool lockWhenDisabled, bool isStoreService)
            {
                Name = name;
                EnableStart = enableStart;
                DisableStart = disableStart;
                LockWhenDisabled = lockWhenDisabled;
                IsStoreService = isStoreService;
            }
        }

        private static readonly ServiceRule[] UpdateServices = new ServiceRule[]
        {
            new ServiceRule("wuauserv", 2, 4, true, false),
            new ServiceRule("dosvc", 2, 4, true, false),
            new ServiceRule("WaaSMedicSvc", 3, 4, true, false),
            new ServiceRule("UsoSvc", 2, 4, true, false),
            new ServiceRule("BITS", 3, 3, false, false),
            new ServiceRule("InstallService", 3, 4, true, true)
        };

        private static readonly string[] CloseProcesses = new string[]
        {
            "MusNotifyIcon",
            "MusNotification",
            "MusNotificationUx",
            "MoNotificationUx",
            "MoUsoCoreWorker"
        };

        private static readonly string[] BlockProcessList = new string[]
        {
            "WaaSMedic.exe",
            "WaasMedicAgent.exe",
            "Windows10Upgrade.exe",
            "Windows10UpgraderApp.exe",
            "UpdateAssistant.exe",
            "UsoClient.exe",
            "remsh.exe",
            "EOSnotify.exe",
            "SihClient.exe",
            "InstallAgent.exe",
            "MusNotification.exe",
            "MusNotificationUx.exe",
            "MoNotificationUx.exe"
        };

        private static readonly string[] TaskPrefixes = new string[]
        {
            @"\Microsoft\Windows\WindowsUpdate\",
            @"\Microsoft\Windows\UpdateOrchestrator\"
        };

        private const string WaasRemediationTask = @"\Microsoft\Windows\WaaSMedic\PerformRemediation";

        public static WindowsUpdateStatusInfo GetStatus()
        {
            WindowsUpdateStatusInfo info = new WindowsUpdateStatusInfo();
            try
            {
                int? storeStart = RegistryAclHelper.GetServiceStartType("InstallService");
                info.IsStoreInstallDisabled = (storeStart.HasValue && storeStart.Value == 4);

                if (!RegistryAclHelper.ServiceKeyExists("wuauserv"))
                {
                    info.State = WindowsUpdateState.NotFound;
                    info.SummaryText = "未找到 Windows Update 服务";
                    info.DetailText = "wuauserv 服务注册表项缺失，可尝试点击「修复更新服务」。";
                    info.Tone = StatusTone.Gray;
                    return info;
                }

                bool readRestricted = RegistryAclHelper.IsServiceKeyReadRestricted("wuauserv");
                int? wuStart = RegistryAclHelper.GetServiceStartType("wuauserv");
                bool isProtected = readRestricted ||
                                   RegistryAclHelper.IsServiceKeyProtected("wuauserv") ||
                                   RegistryAclHelper.IsServiceKeyProtected("WaaSMedicSvc");
                info.IsProtected = isProtected;

                ServiceControllerStatus? scStatus = TryGetServiceStatus("wuauserv");
                string storeTag = info.IsStoreInstallDisabled ? "（微软商店安装服务已禁用）" : "（微软商店正常可用）";

                if (wuStart.HasValue && wuStart.Value == 4)
                {
                    if (isProtected)
                    {
                        info.State = WindowsUpdateState.DisabledProtected;
                        info.SummaryText = "Windows 更新已禁用且已保护";
                        info.DetailText = "服务已禁用并锁定权限防止自动恢复" + storeTag;
                        info.Tone = StatusTone.Red;
                    }
                    else
                    {
                        info.State = WindowsUpdateState.DisabledUnprotected;
                        info.SummaryText = "Windows 更新已禁用（未锁定保护）";
                        info.DetailText = "wuauserv 已设为禁用，但未锁定防恢复权限" + storeTag;
                        info.Tone = StatusTone.Amber;
                    }
                    return info;
                }

                if (readRestricted)
                {
                    info.State = WindowsUpdateState.Unknown;
                    info.SummaryText = "更新服务注册表权限受限（建议点修复或重新禁用）";
                    info.DetailText = "检测到 wuauserv 键值权限异常（可能曾被系统自愈重置），点击「禁用更新」或「修复更新服务」即可自动接管恢复" + storeTag;
                    info.Tone = StatusTone.Amber;
                    return info;
                }

                if (scStatus.HasValue && scStatus.Value == ServiceControllerStatus.Running)
                {
                    info.State = WindowsUpdateState.EnabledRunning;
                    info.SummaryText = "Windows 更新已启用且正在运行";
                    info.DetailText = "wuauserv 处于运行状态" + storeTag;
                    info.Tone = StatusTone.Green;
                }
                else
                {
                    info.State = WindowsUpdateState.EnabledStopped;
                    info.SummaryText = "Windows 更新已启用（待机未运行）";
                    info.DetailText = "wuauserv 启动类型为 " + StartTypeName(wuStart) + storeTag;
                    info.Tone = StatusTone.Green;
                }

                return info;
            }
            catch (Exception ex)
            {
                info.State = WindowsUpdateState.Unknown;
                info.SummaryText = "读取更新状态异常（可点击修复）";
                info.DetailText = ex.Message;
                info.Tone = StatusTone.Amber;
                return info;
            }
        }

        public static OperationResult DisableUpdates(WindowsUpdateOptions options, LogCallback log)
        {
            if (options == null) options = new WindowsUpdateOptions();
            OperationResult result = new OperationResult();

            // 1. 结束更新弹窗/通知进程
            CloseUpdateProcesses(log);

            // 2. 停止并禁用对应服务
            int modified = 0;
            for (int i = 0; i < UpdateServices.Length; i++)
            {
                ServiceRule rule = UpdateServices[i];
                if (rule.IsStoreService && !options.DisableStoreInstallService)
                {
                    if (log != null) log("保留", "未勾选禁用微软商店，保持 InstallService 不变（商店可正常下载更新）");
                    continue;
                }

                if (!RegistryAclHelper.ServiceKeyExists(rule.Name))
                {
                    if (log != null) log("跳过", "未找到服务 " + rule.Name);
                    continue;
                }

                // 先确保解锁再写入
                RegistryAclHelper.UnlockServiceKey(rule.Name, log);

                if (rule.DisableStart == 4)
                {
                    TryStopService(rule.Name, log);
                }

                bool ok = RegistryAclHelper.SetServiceStartType(rule.Name, rule.DisableStart, true, log);
                if (ok)
                {
                    modified++;
                    if (log != null) log("成功", "服务 " + rule.Name + " 已设为 " + StartTypeName(rule.DisableStart) + " (Start=" + rule.DisableStart + ")");
                }

                if (options.ProtectServices && rule.LockWhenDisabled && rule.DisableStart == 4)
                {
                    if (RegistryAclHelper.LockServiceKey(rule.Name, log))
                    {
                        if (log != null) log("成功", "已锁定服务 " + rule.Name + " 注册表权限，防止系统自动恢复");
                    }
                }
            }

            // 3. 禁用更新计划任务
            SetUpdateScheduledTasks(false, log);

            // 4. 拦截更新与升级助手进程 (IFEO)
            int blockedCount = 0;
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                for (int i = 0; i < BlockProcessList.Length; i++)
                {
                    string exe = BlockProcessList[i];
                    try
                    {
                        using (RegistryKey key = hklm.CreateSubKey(IfeoBasePath + exe))
                        {
                            if (key != null)
                            {
                                key.SetValue("Debugger", BenignStub, RegistryValueKind.String);
                                blockedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (log != null) log("警告", "设置进程拦截 " + exe + " 失败: " + ex.Message);
                    }
                }
            }
            if (log != null) log("成功", "已设置 " + blockedCount + " 个更新相关进程拦截 (IFEO)");

            // 5. 写入组策略 NoAutoUpdate
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.CreateSubKey(AuPolicyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("NoAutoUpdate", 1, RegistryValueKind.DWord);
                        key.SetValue("AUOptions", 1, RegistryValueKind.DWord);
                        if (log != null) log("成功", "已写入自动更新禁用策略 (NoAutoUpdate=1)");
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "写入更新组策略失败: " + ex.Message);
            }

            // 6. 隐藏任务栏更新图标
            if (options.HideTrayIcon)
            {
                SetTrayIconVisibility(false, log);
            }

            result.Success = modified > 0;
            result.Message = options.DisableStoreInstallService
                ? "Windows 自动更新（含微软商店安装服务）已禁用。"
                : "Windows 自动更新已禁用（微软商店安装服务已保留）。";
            return result;
        }

        public static OperationResult EnableUpdates(LogCallback log)
        {
            return RestoreUpdatesCore(false, log);
        }

        public static OperationResult RepairServices(LogCallback log)
        {
            return RestoreUpdatesCore(true, log);
        }

        private static OperationResult RestoreUpdatesCore(bool isRepairMode, LogCallback log)
        {
            OperationResult result = new OperationResult();
            int restored = 0;

            // 1. 解除所有更新服务（含 InstallService）的 ACL 锁并恢复默认 Start 类型
            for (int i = 0; i < UpdateServices.Length; i++)
            {
                ServiceRule rule = UpdateServices[i];
                if (!RegistryAclHelper.ServiceKeyExists(rule.Name))
                {
                    if (log != null) log("跳过", "未找到服务 " + rule.Name);
                    continue;
                }

                RegistryAclHelper.UnlockServiceKey(rule.Name, log);
                bool ok = RegistryAclHelper.SetServiceStartType(rule.Name, rule.EnableStart, true, log);
                if (ok)
                {
                    restored++;
                    if (log != null) log("成功", "已解锁并恢复服务 " + rule.Name + " (Start=" + rule.EnableStart + ")");
                }
            }

            // 修复模式下校验并修复 wuauserv 的 ImagePath（兼容旧版 WUB 改动）
            if (isRepairMode)
            {
                VerifyAndFixWuauservImagePath(log);
            }

            // 2. 清理全部更新进程 IFEO 拦截项
            int cleanedIfeo = 0;
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                for (int i = 0; i < BlockProcessList.Length; i++)
                {
                    string exe = BlockProcessList[i];
                    string path = IfeoBasePath + exe;
                    try
                    {
                        bool shouldDeleteKey = false;
                        using (RegistryKey key = hklm.OpenSubKey(path, true))
                        {
                            if (key != null)
                            {
                                if (key.GetValue("Debugger") != null)
                                {
                                    key.DeleteValue("Debugger", false);
                                    cleanedIfeo++;
                                }
                                shouldDeleteKey = (key.ValueCount == 0 && key.SubKeyCount == 0);
                            }
                        }
                        if (shouldDeleteKey)
                        {
                            hklm.DeleteSubKey(path, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (log != null) log("警告", "清理进程拦截 " + exe + " 失败: " + ex.Message);
                    }
                }
            }
            if (log != null) log("成功", "已清理 " + cleanedIfeo + " 个更新进程拦截项");

            // 3. 清理组策略 NoAutoUpdate / AUOptions
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(AuPolicyPath, true))
                {
                    if (key != null)
                    {
                        if (key.GetValue("NoAutoUpdate") != null) key.DeleteValue("NoAutoUpdate", false);
                        if (key.GetValue("AUOptions") != null) key.DeleteValue("AUOptions", false);
                        if (log != null) log("成功", "已清理自动更新禁用组策略");
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "清理更新组策略失败: " + ex.Message);
            }

            // 4. 恢复任务栏更新图标可见性
            SetTrayIconVisibility(true, log);

            // 5. 恢复更新计划任务
            SetUpdateScheduledTasks(true, log);

            // 6. 尝试启动 wuauserv
            TryStartService("wuauserv", log);

            result.Success = restored > 0;
            result.Message = isRepairMode
                ? "Windows 更新服务与相关配置已完成全面修复。"
                : "Windows 自动更新（及微软商店安装服务）已恢复启用。";
            return result;
        }

        private static void VerifyAndFixWuauservImagePath(LogCallback log)
        {
            try
            {
                string subPath = RegistryAclHelper.GetServiceKeyPath("wuauserv");
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(subPath, false))
                {
                    if (key == null) return;
                    string current = key.GetValue("ImagePath", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    if (string.IsNullOrEmpty(current) || current.IndexOf("svchost.exe", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        using (TrustedInstallerHelper.EnterScope())
                        using (RegistryKey writeKey = hklm.OpenSubKey(subPath, true))
                        {
                            if (writeKey != null)
                            {
                                writeKey.SetValue("ImagePath", StandardWuauservImagePath, RegistryValueKind.ExpandString);
                                if (log != null) log("成功", "检测到 wuauserv ImagePath 异常，已自动修复为标准路径");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "校验 wuauserv ImagePath 时出现异常: " + ex.Message);
            }
        }

        private static void SetTrayIconVisibility(bool visible, LogCallback log)
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.CreateSubKey(UxSettingsPath))
                {
                    if (key != null)
                    {
                        if (visible)
                        {
                            if (key.GetValue("TrayIconVisibility") != null)
                            {
                                key.DeleteValue("TrayIconVisibility", false);
                            }
                        }
                        else
                        {
                            key.SetValue("TrayIconVisibility", 0, RegistryValueKind.DWord);
                            if (log != null) log("成功", "已设置隐藏任务栏更新状态图标");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "设置更新托盘图标可见性失败: " + ex.Message);
            }
        }

        private static void CloseUpdateProcesses(LogCallback log)
        {
            int closed = 0;
            for (int i = 0; i < CloseProcesses.Length; i++)
            {
                try
                {
                    Process[] procs = Process.GetProcessesByName(CloseProcesses[i]);
                    for (int j = 0; j < procs.Length; j++)
                    {
                        try
                        {
                            procs[j].Kill();
                            procs[j].WaitForExit(1500);
                            closed++;
                        }
                        catch
                        {
                            // Ignore protected or already exited processes
                        }
                        finally
                        {
                            procs[j].Dispose();
                        }
                    }
                }
                catch
                {
                    // Ignore
                }
            }
            if (closed > 0 && log != null)
            {
                log("成功", "已结束 " + closed + " 个正在运行的更新通知/工作进程");
            }
        }

        private static void SetUpdateScheduledTasks(bool enable, LogCallback log)
        {
            List<string> targetTasks = QueryMatchingScheduledTasks();
            if (targetTasks.Count == 0)
            {
                // 保底尝试直接操作核心任务
                targetTasks.Add(WaasRemediationTask);
            }

            string actionFlag = enable ? "/ENABLE" : "/DISABLE";
            string schtasksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
            int okCount = 0;

            for (int i = 0; i < targetTasks.Count; i++)
            {
                string taskName = targetTasks[i];
                string args = "/Change /TN \"" + taskName + "\" " + actionFlag;

                int exitCode = RunHiddenCommand(schtasksPath, args, 6000);
                if (exitCode != 0)
                {
                    // WaaSMedic 等受保护任务可能拒绝普通管理员，改用 TrustedInstaller 令牌重试
                    int tiExit;
                    if (TrustedInstallerHelper.RunProcessAsTrustedInstaller(schtasksPath, args, 8000, out tiExit) && tiExit == 0)
                    {
                        exitCode = 0;
                    }
                }

                if (exitCode == 0)
                {
                    okCount++;
                }
            }

            if (log != null)
            {
                log("成功", "计划任务处理完成：" + (enable ? "已启用 " : "已禁用 ") + okCount + "/" + targetTasks.Count + " 个更新相关任务");
            }
        }

        private static List<string> QueryMatchingScheduledTasks()
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string schtasksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = schtasksPath;
                psi.Arguments = "/Query /FO CSV /NH";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (!line.StartsWith("\"")) continue;
                        int secondQuote = line.IndexOf('"', 1);
                        if (secondQuote <= 1) continue;

                        string taskName = line.Substring(1, secondQuote - 1);
                        if (IsTargetUpdateTask(taskName) && seen.Add(taskName))
                        {
                            result.Add(taskName);
                        }
                    }
                }
            }
            catch
            {
                // Fallback handled by caller
            }

            return result;
        }

        private static bool IsTargetUpdateTask(string taskName)
        {
            if (string.Equals(taskName, WaasRemediationTask, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            for (int i = 0; i < TaskPrefixes.Length; i++)
            {
                if (taskName.StartsWith(TaskPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static int RunHiddenCommand(string exePath, string args, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exePath;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (p.WaitForExit(timeoutMs))
                    {
                        return p.ExitCode;
                    }
                    return -1;
                }
            }
            catch
            {
                return -1;
            }
        }

        public static void OpenUpdateSettings()
        {
            DefenderManager.OpenUriWithFallback("ms-settings:windowsupdate", "control.exe");
        }

        public static void OpenServicesConsole()
        {
            LaunchShell("services.msc");
        }

        public static void OpenTaskScheduler()
        {
            LaunchShell("taskschd.msc");
        }

        public static void OpenTroubleshooter()
        {
            DefenderManager.OpenUriWithFallback("ms-settings:troubleshoot", "control.exe");
        }

        private static void LaunchShell(string target)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(target);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch
            {
                // Ignore
            }
        }

        private static ServiceControllerStatus? TryGetServiceStatus(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    return sc.Status;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void TryStopService(string serviceName, LogCallback log)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) return;
                    if (sc.CanStop)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                }
            }
            catch
            {
                // Ignore if already stopped or protected
            }
        }

        private static void TryStartService(string serviceName, LogCallback log)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running) return;
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(8));
                    if (log != null) log("成功", "已启动服务 " + serviceName);
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("提示", "未能立即启动 " + serviceName + ": " + ex.Message);
            }
        }

        private static string StartTypeName(int? start)
        {
            if (!start.HasValue) return "未知";
            switch (start.Value)
            {
                case 0: return "引导";
                case 1: return "系统";
                case 2: return "自动";
                case 3: return "手动";
                case 4: return "禁用";
                default: return start.Value.ToString();
            }
        }
    }
}
