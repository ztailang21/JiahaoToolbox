using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace JiahaoToolbox.Core
{
    public static class ElevationHelper
    {
        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 按需通过标准 UAC 提权并原位重启到指定页面。
        /// 调用方在提权进程成功拉起后应立即释放单实例互斥锁并退出当前旧实例。
        /// </summary>
        public static bool TryLaunchElevatedRestart(string targetPage, bool resumeWatcher, out bool userCancelled, out string errorMessage)
        {
            userCancelled = false;
            errorMessage = "";
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string args = string.Format("--page={0} --wait-pid={1}", targetPage ?? "guard", currentPid);
                if (resumeWatcher)
                {
                    args += " --resume-eco";
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = args;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                return true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    userCancelled = true;
                    errorMessage = "已取消管理员权限授权。";
                }
                else
                {
                    errorMessage = "启动管理员进程失败: " + ex.Message;
                }
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "启动管理员进程失败: " + ex.Message;
                return false;
            }
        }
    }
}
