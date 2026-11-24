using System;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace JiahaoToolbox.Core
{
    public static class DefenderManager
    {
        private const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Windows Defender";
        private const string RealtimePolicyPath = @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";
        private const string FeaturesPath = @"SOFTWARE\Microsoft\Windows Defender\Features";
        private const string RealtimePath = @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection";
        private const string IfeoMpCmdRunPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\MpCmdRun.exe";
        private const string BenignStub = @"C:\Windows\System32\systray.exe";

        private struct ServiceTarget
        {
            public string Name;
            public int EnableStart;
            public int DisableStart;

            public ServiceTarget(string name, int enableStart, int disableStart)
            {
                Name = name;
                EnableStart = enableStart;
                DisableStart = disableStart;
            }
        }

        private static readonly ServiceTarget[] DefenderServices = new ServiceTarget[]
        {
            new ServiceTarget("WinDefend", 2, 4),
            new ServiceTarget("WdFilter", 0, 4),
            new ServiceTarget("WdNisDrv", 3, 4),
            new ServiceTarget("WdNisSvc", 3, 4)
        };

        public static TamperState GetTamperProtectionState()
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(FeaturesPath, false))
                {
                    if (key == null) return TamperState.UnsupportedOrUnknown;
                    object val = key.GetValue("TamperProtection");
                    if (val == null) return TamperState.UnsupportedOrUnknown;
                    int num = Convert.ToInt32(val);
                    if (num == 5) return TamperState.Enabled;
                    if (num == 4 || num == 0) return TamperState.Disabled;
                    return TamperState.UnsupportedOrUnknown;
                }
            }
            catch
            {
                return TamperState.UnsupportedOrUnknown;
            }
        }

        public static DefenderStatusInfo GetStatus()
        {
            DefenderStatusInfo info = new DefenderStatusInfo();
            info.Tamper = GetTamperProtectionState();

            if (!RegistryAclHelper.ServiceKeyExists("WinDefend"))
            {
                info.State = DefenderState.NotFound;
                info.SummaryText = "未找到 Windows Defender 服务";
                info.DetailText = "当前系统可能已精简或未安装 Defender。";
                info.Tone = StatusTone.Gray;
                return info;
            }

            int? startVal = RegistryAclHelper.GetServiceStartType("WinDefend");
            ServiceControllerStatus? scStatus = TryGetServiceStatus("WinDefend");
            bool policyDisabled = IsPolicyDisabled();
            bool realtimeOff = IsRealtimeDisabled();

            if (scStatus.HasValue && scStatus.Value == ServiceControllerStatus.Running)
            {
                if (realtimeOff)
                {
                    info.State = DefenderState.RealtimeOff;
                    info.SummaryText = "实时防护已关闭（服务运行中）";
                    info.DetailText = "WinDefend 正在运行，但实时防护处于关闭状态。";
                    info.Tone = StatusTone.Amber;
                }
                else
                {
                    info.State = DefenderState.Running;
                    info.SummaryText = "Windows Defender 运行中";
                    info.DetailText = "核心防病毒服务与实时防护正在运行。";
                    info.Tone = StatusTone.Green;
                }
                return info;
            }

            if ((startVal.HasValue && startVal.Value == 4) || policyDisabled)
            {
                info.State = DefenderState.Disabled;
                info.SummaryText = "Windows Defender 已停用";
                info.DetailText = "服务已停止并设为禁用（Start=4）。";
                info.Tone = StatusTone.Red;
                return info;
            }

            info.State = DefenderState.StoppedButEnabled;
            info.SummaryText = "已配置启用（服务未运行 / 待重启）";
            info.DetailText = "启动项已恢复，若刚刚启用建议重启电脑使驱动加载。";
            info.Tone = StatusTone.Amber;
            return info;
        }

        public static OperationResult DisableDefender(LogCallback log)
        {
            OperationResult result = new OperationResult();
            TamperState tamper = GetTamperProtectionState();
            if (tamper == TamperState.Enabled)
            {
                result.Success = false;
                result.RequiresTamperDisable = true;
                result.Message = "系统已开启「篡改保护」，需先手动关闭后再停用 Defender。";
                if (log != null) log("提示", result.Message);
                return result;
            }

            int changedServices = 0;

            // 1. 写入组策略禁用项
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.CreateSubKey(PolicyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("DisableAntiSpyware", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableAntiVirus", 1, RegistryValueKind.DWord);
                        if (log != null) log("成功", "已写入 Defender 策略禁用项 (DisableAntiSpyware/DisableAntiVirus=1)");
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "写入 Defender 策略项失败: " + ex.Message);
            }

            // 2. IFEO 拦截 MpCmdRun.exe
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.CreateSubKey(IfeoMpCmdRunPath))
                {
                    if (key != null)
                    {
                        key.SetValue("Debugger", BenignStub, RegistryValueKind.String);
                        if (log != null) log("成功", "已设置 MpCmdRun.exe 拦截项 (IFEO)");
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "设置 MpCmdRun.exe 拦截失败: " + ex.Message);
            }

            // 3. 禁用核心服务与内核驱动 Start=4
            for (int i = 0; i < DefenderServices.Length; i++)
            {
                ServiceTarget svc = DefenderServices[i];
                if (!RegistryAclHelper.ServiceKeyExists(svc.Name))
                {
                    if (log != null) log("跳过", "未找到服务 " + svc.Name);
                    continue;
                }

                bool ok = RegistryAclHelper.SetServiceStartType(svc.Name, svc.DisableStart, true, log);
                if (ok)
                {
                    changedServices++;
                    if (log != null) log("成功", "服务 " + svc.Name + " 已设为禁用 (Start=4)");
                }
            }

            // 4. 尝试平滑停止正在运行的服务（WinDefend 受 PPL 保护时可能需重启彻底停止）
            TryStopService("WdNisSvc", log);
            bool winDefendStopped = TryStopService("WinDefend", log);
            if (!winDefendStopped)
            {
                ServiceControllerStatus? st = TryGetServiceStatus("WinDefend");
                if (st.HasValue && st.Value == ServiceControllerStatus.Running)
                {
                    result.RebootRecommended = true;
                    if (log != null) log("提示", "WinDefend 受系统进程保护仍在运行，禁用配置已写入，重启电脑后将彻底停用。");
                }
            }

            result.Success = changedServices > 0;
            result.Message = result.RebootRecommended
                ? "已写入 Defender 停用配置，建议重启电脑以彻底生效。"
                : "Windows Defender 已成功停用。";
            return result;
        }

        public static OperationResult EnableDefender(LogCallback log)
        {
            OperationResult result = new OperationResult();
            int restoredServices = 0;

            // 1. 清理组策略禁用项
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (RegistryKey key = hklm.OpenSubKey(PolicyPath, true))
                    {
                        if (key != null)
                        {
                            DeleteValueSafe(key, "DisableAntiSpyware");
                            DeleteValueSafe(key, "DisableAntiVirus");
                        }
                    }
                    using (RegistryKey rtKey = hklm.OpenSubKey(RealtimePolicyPath, true))
                    {
                        if (rtKey != null)
                        {
                            DeleteValueSafe(rtKey, "DisableRealtimeMonitoring");
                        }
                    }
                    if (log != null) log("成功", "已清理 Defender 禁用策略注册表项");
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "清理 Defender 策略项失败: " + ex.Message);
            }

            // 2. 清理 MpCmdRun.exe IFEO 拦截项
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    bool shouldDeleteKey = false;
                    using (RegistryKey key = hklm.OpenSubKey(IfeoMpCmdRunPath, true))
                    {
                        if (key != null)
                        {
                            DeleteValueSafe(key, "Debugger");
                            shouldDeleteKey = (key.ValueCount == 0 && key.SubKeyCount == 0);
                        }
                    }
                    if (shouldDeleteKey)
                    {
                        hklm.DeleteSubKey(IfeoMpCmdRunPath, false);
                    }
                    if (log != null) log("成功", "已清理 MpCmdRun.exe 拦截项");
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "清理 MpCmdRun.exe 拦截项失败: " + ex.Message);
            }

            // 3. 恢复核心服务与驱动默认 Start 值
            for (int i = 0; i < DefenderServices.Length; i++)
            {
                ServiceTarget svc = DefenderServices[i];
                if (!RegistryAclHelper.ServiceKeyExists(svc.Name))
                {
                    if (log != null) log("跳过", "未找到服务 " + svc.Name);
                    continue;
                }

                bool ok = RegistryAclHelper.SetServiceStartType(svc.Name, svc.EnableStart, true, log);
                if (ok)
                {
                    restoredServices++;
                    if (log != null) log("成功", "服务 " + svc.Name + " 已恢复默认启动类型 (Start=" + svc.EnableStart + ")");
                }
            }

            // 4. 尝试立即启动 WinDefend 与 WdNisSvc
            bool started = TryStartService("WinDefend", log);
            if (started)
            {
                TryStartService("WdNisSvc", log);
            }
            else
            {
                result.RebootRecommended = true;
                if (log != null) log("提示", "服务与驱动配置已恢复；因内核过滤驱动 WdFilter 需随开机引导加载，请重启电脑完成启用。");
            }

            result.Success = restoredServices > 0;
            result.Message = result.RebootRecommended
                ? "已恢复 Windows Defender 配置，请重启电脑使防病毒服务正常启动。"
                : "Windows Defender 已成功启用并运行。";
            return result;
        }

        public static OperationResult AddSelfToExclusions(LogCallback log)
        {
            OperationResult result = new OperationResult();
            string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            string escaped = dir.Replace("'", "''");
            string cmd = "Add-MpPreference -ExclusionPath '" + escaped + "'";

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + cmd + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    if (p.ExitCode == 0)
                    {
                        result.Success = true;
                        result.Message = "已将当前程序目录加入 Defender 排除列表: " + dir;
                        if (log != null) log("成功", result.Message);
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "添加排除项失败（若 Defender 当前已停用，需先启用后再添加）。";
                        if (log != null) log("警告", result.Message + (string.IsNullOrWhiteSpace(stderr) ? "" : " " + stderr.Trim()));
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "调用 PowerShell 添加排除项失败: " + ex.Message;
                if (log != null) log("失败", result.Message);
            }
            return result;
        }

        public static void OpenSecurityCenter()
        {
            OpenUriWithFallback("windowsdefender://", "ms-settings:windowsdefender");
        }

        public static void OpenTamperProtectionSettings()
        {
            OpenUriWithFallback("windowsdefender://threatsettings", "ms-settings:windowsdefender");
        }

        internal static void OpenUriWithFallback(string primaryUri, string fallbackUri)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(primaryUri);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch
            {
                try
                {
                    ProcessStartInfo psi2 = new ProcessStartInfo(fallbackUri);
                    psi2.UseShellExecute = true;
                    Process.Start(psi2);
                }
                catch
                {
                    // Ignore shell launch error
                }
            }
        }

        private static bool IsPolicyDisabled()
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(PolicyPath, false))
                {
                    if (key == null) return false;
                    object v1 = key.GetValue("DisableAntiSpyware");
                    object v2 = key.GetValue("DisableAntiVirus");
                    return (v1 != null && Convert.ToInt32(v1) == 1) || (v2 != null && Convert.ToInt32(v2) == 1);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRealtimeDisabled()
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (RegistryKey k1 = hklm.OpenSubKey(RealtimePolicyPath, false))
                    {
                        if (k1 != null)
                        {
                            object v = k1.GetValue("DisableRealtimeMonitoring");
                            if (v != null && Convert.ToInt32(v) == 1) return true;
                        }
                    }
                    using (RegistryKey k2 = hklm.OpenSubKey(RealtimePath, false))
                    {
                        if (k2 != null)
                        {
                            object v = k2.GetValue("DisableRealtimeMonitoring");
                            if (v != null && Convert.ToInt32(v) == 1) return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }
            return false;
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

        private static bool TryStopService(string serviceName, LogCallback log)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) return true;
                    if (sc.CanStop)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(6));
                        if (log != null) log("成功", "已停止服务 " + serviceName);
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryStartService(string serviceName, LogCallback log)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running) return true;
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(8));
                    if (log != null) log("成功", "已启动服务 " + serviceName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("提示", "未能立即启动 " + serviceName + ": " + ex.Message);
                return false;
            }
        }

        private static void DeleteValueSafe(RegistryKey key, string valueName)
        {
            try
            {
                if (key.GetValue(valueName) != null)
                {
                    key.DeleteValue(valueName, false);
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}
