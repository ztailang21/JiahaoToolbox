using System;
using Microsoft.Win32;

namespace JiahaoToolbox.Core
{
    public static class SmartAppControlManager
    {
        private const string CiPolicyPath = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
        private const string PolicyValueName = "VerifiedAndReputablePolicyState";

        public static SmartAppControlStatusInfo GetStatus()
        {
            SmartAppControlStatusInfo info = new SmartAppControlStatusInfo();
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(CiPolicyPath, false))
                {
                    if (key == null)
                    {
                        return BuildUnsupported();
                    }

                    object raw = key.GetValue(PolicyValueName);
                    if (raw == null)
                    {
                        return BuildUnsupported();
                    }

                    int val = Convert.ToInt32(raw);
                    info.RawValue = val;
                    switch (val)
                    {
                        case 1:
                            info.State = SmartAppControlState.On;
                            info.SummaryText = "已开启（强制保护模式）";
                            info.DetailText = "仅允许受信任/签名良好的应用运行 (VerifiedAndReputablePolicyState=1)。";
                            info.Tone = StatusTone.Green;
                            break;
                        case 2:
                            info.State = SmartAppControlState.Evaluation;
                            info.SummaryText = "评估模式（Evaluation）";
                            info.DetailText = "系统正在后台评估是否启用智能应用控制 (VerifiedAndReputablePolicyState=2)。";
                            info.Tone = StatusTone.Amber;
                            break;
                        case 0:
                            info.State = SmartAppControlState.Off;
                            info.SummaryText = "已关闭";
                            info.DetailText = "智能应用控制当前处于关闭状态 (VerifiedAndReputablePolicyState=0)。";
                            info.Tone = StatusTone.Red;
                            break;
                        default:
                            info.State = SmartAppControlState.Unsupported;
                            info.SummaryText = "未知状态 (" + val + ")";
                            info.DetailText = "检测到非标准策略值。";
                            info.Tone = StatusTone.Gray;
                            break;
                    }
                    return info;
                }
            }
            catch
            {
                return BuildUnsupported();
            }
        }

        private static SmartAppControlStatusInfo BuildUnsupported()
        {
            SmartAppControlStatusInfo info = new SmartAppControlStatusInfo();
            info.State = SmartAppControlState.Unsupported;
            info.RawValue = null;
            info.SummaryText = "当前系统不支持 / 未配置";
            info.DetailText = "仅 Windows 11 22H2 及以上版本包含智能应用控制策略键值。";
            info.Tone = StatusTone.Gray;
            return info;
        }

        /// <summary>
        /// 修改智能应用控制策略值（0=关闭，2=评估模式，1=开启）。需重启生效。
        /// </summary>
        public static OperationResult SetPolicyState(int targetValue, LogCallback log)
        {
            OperationResult result = new OperationResult();
            SmartAppControlStatusInfo current = GetStatus();
            if (current.State == SmartAppControlState.Unsupported)
            {
                result.Success = false;
                result.Message = "当前系统不包含智能应用控制策略项，已跳过修改。";
                if (log != null) log("跳过", result.Message);
                return result;
            }

            string targetLabel = targetValue == 0 ? "关闭 (0)" : (targetValue == 2 ? "评估模式 (2)" : "开启 (1)");

            // 先尝试使用 TrustedInstaller 权限写入，失败时回退普通管理员写入
            bool written = false;
            Exception lastEx = null;

            try
            {
                using (TrustedInstallerHelper.EnterScope())
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(CiPolicyPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue(PolicyValueName, targetValue, RegistryValueKind.DWord);
                        written = true;
                    }
                }
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }

            if (!written)
            {
                try
                {
                    using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (RegistryKey key = hklm.OpenSubKey(CiPolicyPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue(PolicyValueName, targetValue, RegistryValueKind.DWord);
                            written = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }

            if (written)
            {
                result.Success = true;
                result.RebootRecommended = true;
                result.Message = "已将智能应用控制设为 " + targetLabel + "，请重启电脑使更改生效。";
                if (log != null) log("成功", result.Message);
            }
            else
            {
                result.Success = false;
                result.Message = "修改智能应用控制失败（可能受系统篡改保护或安全策略限制，建议点「打开系统设置」手动调整）。";
                if (log != null) log("失败", result.Message + (lastEx != null ? " 原因: " + lastEx.Message : ""));
            }

            return result;
        }

        public static void OpenSmartAppSettings()
        {
            DefenderManager.OpenUriWithFallback("windowsdefender://smartapp", "ms-settings:windowsdefender");
        }
    }
}
