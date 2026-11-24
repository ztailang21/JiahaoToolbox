using System;
using System.Diagnostics;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace JiahaoToolbox.Core
{
    public static class RegistryAclHelper
    {
        private static readonly SecurityIdentifier SystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier AdminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier UsersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        private static readonly SecurityIdentifier WorldSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        private const RegistryRights LockDenyRights =
            RegistryRights.SetValue |
            RegistryRights.CreateSubKey |
            RegistryRights.Delete |
            RegistryRights.ChangePermissions |
            RegistryRights.TakeOwnership;

        public static string GetServiceKeyPath(string serviceName)
        {
            return @"SYSTEM\CurrentControlSet\Services\" + serviceName;
        }

        /// <summary>
        /// 安全检测服务注册表项是否存在。若触发 SecurityException / UnauthorizedAccessException，
        /// 说明键值确实存在但权限受限，返回 true 而不是抛出未处理异常。
        /// </summary>
        public static bool ServiceKeyExists(string serviceName)
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(GetServiceKeyPath(serviceName), false))
                {
                    return key != null;
                }
            }
            catch (SecurityException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检测服务注册表项当前是否因 ACL 受限而无法读取。
        /// </summary>
        public static bool IsServiceKeyReadRestricted(string serviceName)
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(GetServiceKeyPath(serviceName), false))
                {
                    return false;
                }
            }
            catch (SecurityException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int? GetServiceStartType(string serviceName)
        {
            // 1. 直接读取注册表
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(GetServiceKeyPath(serviceName), false))
                {
                    if (key == null) return null;
                    object val = key.GetValue("Start");
                    if (val == null) return null;
                    return Convert.ToInt32(val);
                }
            }
            catch (SecurityException)
            {
                // 权限受限时走下方回退查询
            }
            catch (UnauthorizedAccessException)
            {
                // 权限受限时走下方回退查询
            }
            catch
            {
                return null;
            }

            // 2. 回退：通过 sc qc 从 SCM 读取启动类型（只读，不修改 ACL）
            return QueryStartTypeFromScm(serviceName);
        }

        private static int? QueryStartTypeFromScm(string serviceName)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = "qc \"" + serviceName + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.IndexOf("START_TYPE", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            int colon = line.IndexOf(':');
                            if (colon >= 0 && colon + 1 < line.Length)
                            {
                                string rest = line.Substring(colon + 1).Trim();
                                int sp = rest.IndexOf(' ');
                                string numToken = sp > 0 ? rest.Substring(0, sp) : rest;
                                int parsed;
                                if (int.TryParse(numToken, out parsed))
                                {
                                    return parsed;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }
            return null;
        }

        /// <summary>
        /// 检测服务注册表项是否被 ACL 锁定保护（兼容本工具、WUB 或系统自身施加的受限 ACL）。
        /// </summary>
        public static bool IsServiceKeyProtected(string serviceName)
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(GetServiceKeyPath(serviceName), RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadPermissions))
                {
                    if (key == null) return false;
                    RegistrySecurity sec = key.GetAccessControl(AccessControlSections.Access);
                    AuthorizationRuleCollection rules = sec.GetAccessRules(true, true, typeof(SecurityIdentifier));
                    bool systemCanWrite = false;

                    foreach (AuthorizationRule rawRule in rules)
                    {
                        RegistryAccessRule rule = rawRule as RegistryAccessRule;
                        if (rule == null) continue;
                        SecurityIdentifier sid = rule.IdentityReference as SecurityIdentifier;
                        if (sid == null) continue;

                        if (rule.AccessControlType == AccessControlType.Deny)
                        {
                            if ((sid.Equals(SystemSid) || sid.Equals(WorldSid)) &&
                                (rule.RegistryRights & RegistryRights.SetValue) == RegistryRights.SetValue)
                            {
                                return true;
                            }
                        }
                        else if (rule.AccessControlType == AccessControlType.Allow)
                        {
                            if (sid.Equals(SystemSid) &&
                                (rule.RegistryRights & RegistryRights.SetValue) == RegistryRights.SetValue)
                            {
                                systemCanWrite = true;
                            }
                        }
                    }

                    if (!systemCanWrite && rules.Count > 0)
                    {
                        return true;
                    }
                    return false;
                }
            }
            catch (SecurityException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 解除服务注册表项的所有 Deny/受限 ACL 锁，恢复 Administrators/SYSTEM 完全控制与 Users 读取权限。
        /// 优先以管理员身份直接接管（不受 Deny SYSTEM 限制），失败时回退 TrustedInstaller 权限。
        /// </summary>
        public static bool UnlockServiceKey(string serviceName, LogCallback log)
        {
            string subPath = GetServiceKeyPath(serviceName);
            if (!ServiceKeyExists(serviceName))
            {
                return false;
            }

            TrustedInstallerHelper.EnableProcessPrivilege("SeTakeOwnershipPrivilege");
            TrustedInstallerHelper.EnableProcessPrivilege("SeRestorePrivilege");
            TrustedInstallerHelper.EnableProcessPrivilege("SeBackupPrivilege");

            if (TryUnlockInternal(subPath))
            {
                return true;
            }

            try
            {
                using (TrustedInstallerHelper.EnterScope())
                {
                    if (TryUnlockInternal(subPath))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "解锁服务键 " + serviceName + " 失败: " + ex.Message);
            }

            return false;
        }

        private static bool TryUnlockInternal(string subPath)
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    // 第一步：将所有者设为 Administrators（拥有 TakeOwnership 特权时即使 DACL 完全拒绝也能接管）
                    try
                    {
                        using (RegistryKey ownerKey = hklm.OpenSubKey(subPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
                        {
                            if (ownerKey != null)
                            {
                                RegistrySecurity ownerSec = new RegistrySecurity();
                                ownerSec.SetOwner(AdminsSid);
                                ownerKey.SetAccessControl(ownerSec);
                            }
                        }
                    }
                    catch
                    {
                        // 若已有 ChangePermissions 权限则继续
                    }

                    // 第二步：先仅以 ChangePermissions 打开（不请求 ReadPermissions，防止极严苛 DACL 拒绝读取）
                    using (RegistryKey aclKey = hklm.OpenSubKey(subPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
                    {
                        if (aclKey == null) return false;

                        RegistrySecurity cleanSec = new RegistrySecurity();
                        cleanSec.SetAccessRuleProtection(false, false);
                        cleanSec.AddAccessRule(new RegistryAccessRule(
                            AdminsSid,
                            RegistryRights.FullControl,
                            InheritanceFlags.ContainerInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));
                        cleanSec.AddAccessRule(new RegistryAccessRule(
                            SystemSid,
                            RegistryRights.FullControl,
                            InheritanceFlags.ContainerInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));
                        cleanSec.AddAccessRule(new RegistryAccessRule(
                            UsersSid,
                            RegistryRights.ReadKey,
                            InheritanceFlags.ContainerInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));

                        aclKey.SetAccessControl(cleanSec);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 强化锁定服务注册表项：
        /// - 切断继承并彻底移除 SYSTEM 的写入与改权限能力（同时显式 Deny 写入/改权/接管），防止 WaaSMedic/SCM 自行改回
        /// - 保留 Administrators 完全控制与 Users/Everyone 只读，确保本工具可随时无损解锁且查询状态永不报权限错误
        /// </summary>
        public static bool LockServiceKey(string serviceName, LogCallback log)
        {
            string subPath = GetServiceKeyPath(serviceName);
            if (!ServiceKeyExists(serviceName))
            {
                return false;
            }

            UnlockServiceKey(serviceName, log);

            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey aclKey = hklm.OpenSubKey(subPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
                {
                    if (aclKey == null) return false;

                    RegistrySecurity sec = new RegistrySecurity();
                    // 禁用从父键继承，避免继承到 SYSTEM FullControl
                    sec.SetAccessRuleProtection(true, false);

                    sec.AddAccessRule(new RegistryAccessRule(
                        AdminsSid,
                        RegistryRights.FullControl,
                        InheritanceFlags.ContainerInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                    sec.AddAccessRule(new RegistryAccessRule(
                        UsersSid,
                        RegistryRights.ReadKey,
                        InheritanceFlags.ContainerInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                    sec.AddAccessRule(new RegistryAccessRule(
                        WorldSid,
                        RegistryRights.ReadKey,
                        InheritanceFlags.ContainerInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                    sec.AddAccessRule(new RegistryAccessRule(
                        SystemSid,
                        RegistryRights.ReadKey,
                        InheritanceFlags.ContainerInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                    sec.AddAccessRule(new RegistryAccessRule(
                        SystemSid,
                        LockDenyRights,
                        InheritanceFlags.None,
                        PropagationFlags.None,
                        AccessControlType.Deny));

                    aclKey.SetAccessControl(sec);
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "锁定服务键 " + serviceName + " 失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 写入服务 Start 值（自动解锁受限服务键，按需使用 TrustedInstaller 权限）。
        /// </summary>
        public static bool SetServiceStartType(string serviceName, int startValue, bool preferTrustedInstaller, LogCallback log)
        {
            string subPath = GetServiceKeyPath(serviceName);
            if (!ServiceKeyExists(serviceName))
            {
                if (log != null) log("跳过", "未找到服务 " + serviceName);
                return false;
            }

            if (IsServiceKeyProtected(serviceName) || IsServiceKeyReadRestricted(serviceName))
            {
                UnlockServiceKey(serviceName, log);
            }

            if (!preferTrustedInstaller)
            {
                if (TryWriteStart(subPath, startValue))
                {
                    return true;
                }
            }

            try
            {
                using (TrustedInstallerHelper.EnterScope())
                {
                    if (TryWriteStart(subPath, startValue))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("警告", "TrustedInstaller 写入 " + serviceName + " 异常: " + ex.Message);
            }

            UnlockServiceKey(serviceName, log);
            if (TryWriteStart(subPath, startValue))
            {
                return true;
            }

            if (log != null) log("失败", "无法修改服务 " + serviceName + " 的 Start=" + startValue);
            return false;
        }

        private static bool TryWriteStart(string subPath, int startValue)
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey(subPath, true))
                {
                    if (key == null) return false;
                    key.SetValue("Start", startValue, RegistryValueKind.DWord);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
