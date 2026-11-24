using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace JiahaoToolbox.Core
{
    public static class AutostartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "JiahaoToolbox";
        private const string LegacyEcoValueName = "AutoEcoControl";

        public static string LegacyVbsShortcutPath
        {
            get
            {
                string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                return Path.Combine(startup, "AutoEfficiencyMode.lnk");
            }
        }

        public static bool IsAutostartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string val = key.GetValue(ValueName) as string;
                    return !string.IsNullOrWhiteSpace(val);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool HasLegacyAutostart()
        {
            try
            {
                if (File.Exists(LegacyVbsShortcutPath)) return true;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null && !string.IsNullOrWhiteSpace(key.GetValue(LegacyEcoValueName) as string))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool SetAutostart(bool enable, out string detail)
        {
            detail = "";
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        detail = "无法打开当前用户启动项注册表";
                        return false;
                    }

                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        string cmd = string.Format("\"{0}\" --tray", exePath);
                        key.SetValue(ValueName, cmd, RegistryValueKind.String);

                        bool cleanedLegacy = false;
                        if (key.GetValue(LegacyEcoValueName) != null)
                        {
                            key.DeleteValue(LegacyEcoValueName, false);
                            cleanedLegacy = true;
                        }
                        try
                        {
                            if (File.Exists(LegacyVbsShortcutPath))
                            {
                                File.Delete(LegacyVbsShortcutPath);
                                cleanedLegacy = true;
                            }
                        }
                        catch { }

                        detail = cleanedLegacy
                            ? "已开启嘉豪工具箱开机自启（静默托盘模式），并已自动清理旧版 AutoEcoControl 启动项避免冲突"
                            : "已开启嘉豪工具箱开机自启（静默托盘模式，免 UAC 弹窗）";
                    }
                    else
                    {
                        if (key.GetValue(ValueName) != null)
                        {
                            key.DeleteValue(ValueName, false);
                        }
                        detail = "已关闭开机自启";
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                detail = "修改开机自启失败: " + ex.Message;
                return false;
            }
        }
    }
}
