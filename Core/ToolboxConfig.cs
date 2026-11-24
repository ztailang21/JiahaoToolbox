using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace JiahaoToolbox.Core
{
    public sealed class ToolboxConfig
    {
        public const int CurrentConfigVersion = 1;

        public int ConfigVersion { get; set; }

        // Eco Mode Settings
        public bool AutoStartWatcher { get; set; }
        public bool EnableEfficiencyMode { get; set; }
        public bool EnableForegroundExemption { get; set; }
        public string PriorityClass { get; set; }
        public bool EnableIdleMemoryTrim { get; set; }
        public int PollIntervalSeconds { get; set; }
        public int IdleTrimDelaySeconds { get; set; }
        public int TrimCooldownSeconds { get; set; }
        public double TrimMinWorkingSetMb { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }
        public List<TargetAppEntry> Apps { get; set; }

        // Windows Update / Guard Settings
        public bool ProtectServices { get; set; }
        public bool DisableStoreInstallService { get; set; }
        public bool HideTrayIcon { get; set; }

        // Migration metadata
        public bool MigratedFromLegacyEco { get; set; }

        public ToolboxConfig()
        {
            ConfigVersion = CurrentConfigVersion;

            AutoStartWatcher = false;
            EnableEfficiencyMode = true;
            EnableForegroundExemption = true;
            PriorityClass = "idle";
            EnableIdleMemoryTrim = false;
            PollIntervalSeconds = 4;
            IdleTrimDelaySeconds = 180;
            TrimCooldownSeconds = 600;
            TrimMinWorkingSetMb = 80.0;
            MinimizeToTrayOnClose = true;
            Apps = new List<TargetAppEntry>();

            ProtectServices = true;
            DisableStoreInstallService = false;
            HideTrayIcon = true;
        }

        public static string ConfigDir
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "JiahaoToolbox");
            }
        }

        public static string ConfigFilePath
        {
            get { return Path.Combine(ConfigDir, "config.json"); }
        }

        public static string LegacyEcoConfigPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(Path.Combine(appData, "AutoEcoControl"), "config.json");
            }
        }

        public static string LegacyGuardConfigPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(Path.Combine(appData, "WinGuardUpdateControl"), "config.json");
            }
        }

        public static bool HasLegacyEcoConfig()
        {
            try { return File.Exists(LegacyEcoConfigPath); } catch { return false; }
        }

        public static ToolboxConfig CreateDefault()
        {
            ToolboxConfig cfg = new ToolboxConfig();
            cfg.Apps.Add(new TargetAppEntry
            {
                Id = "preset_wechat",
                DisplayName = "微信",
                PrimaryExe = "Weixin.exe",
                InstallDir = "",
                Enabled = false,
                KnownExes = new List<string>
                {
                    "Weixin.exe", "WeChat.exe", "WeChatAppEx.exe", "WeChatPlayer.exe", "WeChatUtility.exe"
                }
            });
            cfg.Apps.Add(new TargetAppEntry
            {
                Id = "preset_baidunetdisk",
                DisplayName = "百度网盘",
                PrimaryExe = "BaiduNetdisk.exe",
                InstallDir = "",
                Enabled = false,
                KnownExes = new List<string>
                {
                    "BaiduNetdisk.exe", "BaiduNetdiskRender.exe", "baidunetdiskhost.exe",
                    "BaiduNetdiskUnite.exe", "BaiduNetdiskImageViewer.exe",
                    "YunDetectService.exe", "YunUtilityService.exe"
                }
            });
            cfg.Apps.Add(new TargetAppEntry
            {
                Id = "preset_quark",
                DisplayName = "夸克网盘",
                PrimaryExe = "QuarkCloudDrive.exe",
                InstallDir = "",
                Enabled = false,
                KnownExes = new List<string>
                {
                    "quark.exe", "new_quark.exe", "quark_proxy.exe", "new_quark_proxy.exe",
                    "QuarkCloudDrive.exe", "accelerator.exe", "clouddrive_desktop_widget.exe",
                    "media_container.exe", "notification_helper.exe"
                }
            });
            return cfg;
        }

        public static ToolboxConfig Load(bool readOnlyMode)
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    ToolboxConfig cfg = serializer.Deserialize<ToolboxConfig>(json);
                    if (cfg != null)
                    {
                        Normalize(cfg);
                        return cfg;
                    }
                }
            }
            catch
            {
                // Fall back to fresh/migrated config
            }

            ToolboxConfig fresh = CreateDefault();
            TryImportLegacyConfigs(fresh);
            Normalize(fresh);
            if (!readOnlyMode)
            {
                fresh.Save();
            }
            return fresh;
        }

        public static ToolboxConfig Load()
        {
            return Load(false);
        }

        public static int TryImportLegacyConfigs(ToolboxConfig target)
        {
            int importedApps = 0;
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            // 1. Import AutoEcoControl config if present
            try
            {
                if (File.Exists(LegacyEcoConfigPath))
                {
                    string json = File.ReadAllText(LegacyEcoConfigPath, Encoding.UTF8);
                    ToolboxConfig legacy = serializer.Deserialize<ToolboxConfig>(json);
                    if (legacy != null)
                    {
                        target.AutoStartWatcher = legacy.AutoStartWatcher;
                        target.EnableEfficiencyMode = legacy.EnableEfficiencyMode;
                        target.EnableForegroundExemption = legacy.EnableForegroundExemption;
                        if (!string.IsNullOrEmpty(legacy.PriorityClass)) target.PriorityClass = legacy.PriorityClass;
                        target.EnableIdleMemoryTrim = legacy.EnableIdleMemoryTrim;
                        if (legacy.PollIntervalSeconds >= 2) target.PollIntervalSeconds = legacy.PollIntervalSeconds;
                        target.MinimizeToTrayOnClose = legacy.MinimizeToTrayOnClose;

                        if (legacy.Apps != null && legacy.Apps.Count > 0)
                        {
                            target.Apps = new List<TargetAppEntry>(legacy.Apps);
                            importedApps = target.Apps.Count;
                        }
                        target.MigratedFromLegacyEco = true;
                    }
                }
            }
            catch
            {
                // Non-fatal
            }

            // 2. Import WinGuardUpdateControl config if present
            try
            {
                if (File.Exists(LegacyGuardConfigPath))
                {
                    string json = File.ReadAllText(LegacyGuardConfigPath, Encoding.UTF8);
                    Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null)
                    {
                        object v;
                        if (dict.TryGetValue("ProtectServices", out v) && v is bool) target.ProtectServices = (bool)v;
                        if (dict.TryGetValue("DisableStoreInstallService", out v) && v is bool) target.DisableStoreInstallService = (bool)v;
                        if (dict.TryGetValue("HideTrayIcon", out v) && v is bool) target.HideTrayIcon = (bool)v;
                    }
                }
            }
            catch
            {
                // Non-fatal
            }

            return importedApps;
        }

        private static void Normalize(ToolboxConfig cfg)
        {
            if (cfg.Apps == null) cfg.Apps = new List<TargetAppEntry>();
            for (int i = 0; i < cfg.Apps.Count; i++)
            {
                TargetAppEntry a = cfg.Apps[i];
                if (string.IsNullOrEmpty(a.Id)) a.Id = Guid.NewGuid().ToString("N");
                if (a.KnownExes == null) a.KnownExes = new List<string>();
                if (!string.IsNullOrEmpty(a.PrimaryExe) && !ContainsIgnoreCase(a.KnownExes, a.PrimaryExe))
                {
                    a.KnownExes.Insert(0, a.PrimaryExe);
                }
            }
            if (cfg.PollIntervalSeconds < 2) cfg.PollIntervalSeconds = 4;
            if (cfg.IdleTrimDelaySeconds < 30) cfg.IdleTrimDelaySeconds = 180;
            if (cfg.TrimCooldownSeconds < 60) cfg.TrimCooldownSeconds = 600;
            if (cfg.TrimMinWorkingSetMb < 20) cfg.TrimMinWorkingSetMb = 80;
            if (string.IsNullOrEmpty(cfg.PriorityClass)) cfg.PriorityClass = "idle";
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                string tmp = ConfigFilePath + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
                File.Move(tmp, ConfigFilePath);
            }
            catch
            {
                // Ignore transient IO failures
            }
        }

        public int EnabledAppCount()
        {
            int count = 0;
            if (Apps != null)
            {
                for (int i = 0; i < Apps.Count; i++)
                {
                    if (Apps[i].Enabled) count++;
                }
            }
            return count;
        }

        public static bool ContainsIgnoreCase(List<string> list, string value)
        {
            if (list == null || string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
