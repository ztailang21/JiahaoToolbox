using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;

namespace JiahaoToolbox.Core
{
    public static class TrustedInstallerHelper
    {
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_IMPERSONATE = 0x0004;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint MAXIMUM_ALLOWED = 0x02000000;

        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const int TokenImpersonation = 2;

        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint STARTF_USESHOWWINDOW = 0x00000001;
        private const short SW_HIDE = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenThreadToken(IntPtr ThreadHandle, uint DesiredAccess, bool OpenAsSelf, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ImpersonateLoggedOnUser(IntPtr hToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool RevertToSelf();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithTokenW(
            IntPtr hToken,
            uint dwLogonFlags,
            string lpApplicationName,
            string lpCommandLine,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        public static bool IsCurrentProcessAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool EnableProcessPrivilege(string privilegeName)
        {
            IntPtr hToken;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
            {
                return false;
            }
            try
            {
                return EnablePrivilegeOnToken(hToken, privilegeName);
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        private static bool EnableThreadPrivilege(string privilegeName)
        {
            IntPtr hToken;
            if (!OpenThreadToken(GetCurrentThread(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, false, out hToken))
            {
                return false;
            }
            try
            {
                return EnablePrivilegeOnToken(hToken, privilegeName);
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        private static bool EnablePrivilegeOnToken(IntPtr hToken, string privilegeName)
        {
            LUID luid;
            if (!LookupPrivilegeValue(null, privilegeName, out luid))
            {
                return false;
            }
            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
            tp.PrivilegeCount = 1;
            tp.Luid = luid;
            tp.Attributes = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                return false;
            }
            int err = Marshal.GetLastWin32Error();
            return err == 0;
        }

        public static int EnsureTrustedInstallerRunning()
        {
            using (ServiceController sc = new ServiceController("TrustedInstaller"))
            {
                if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
                {
                    if (sc.Status == ServiceControllerStatus.StopPending)
                    {
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                    sc.Start();
                }
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(8));
            }

            for (int i = 0; i < 20; i++)
            {
                Process[] procs = Process.GetProcessesByName("TrustedInstaller");
                if (procs.Length > 0)
                {
                    int pid = procs[0].Id;
                    for (int j = 0; j < procs.Length; j++) procs[j].Dispose();
                    return pid;
                }
                Thread.Sleep(100);
            }
            throw new InvalidOperationException("TrustedInstaller 服务已启动但未找到 TrustedInstaller.exe 进程。");
        }

        private static IntPtr DuplicateTokenFromProcess(int pid, int tokenType)
        {
            IntPtr hProc = OpenProcess(PROCESS_QUERY_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero)
            {
                hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            }
            if (hProc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开目标系统进程 PID=" + pid);
            }

            IntPtr hToken = IntPtr.Zero;
            IntPtr hDup = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(hProc, TOKEN_DUPLICATE | TOKEN_QUERY, out hToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开目标进程令牌 PID=" + pid);
                }
                if (!DuplicateTokenEx(hToken, MAXIMUM_ALLOWED, IntPtr.Zero, SecurityImpersonation, tokenType, out hDup))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法复制目标进程令牌 PID=" + pid);
                }
                return hDup;
            }
            finally
            {
                if (hToken != IntPtr.Zero) CloseHandle(hToken);
                CloseHandle(hProc);
            }
        }

        private static int FindSystemProcessPid()
        {
            string[] candidates = new string[] { "winlogon", "lsass", "services" };
            int currentSession = Process.GetCurrentProcess().SessionId;

            for (int c = 0; c < candidates.Length; c++)
            {
                Process[] procs = Process.GetProcessesByName(candidates[c]);
                int fallbackPid = -1;
                try
                {
                    for (int i = 0; i < procs.Length; i++)
                    {
                        if (procs[i].SessionId == currentSession)
                        {
                            return procs[i].Id;
                        }
                        if (fallbackPid < 0)
                        {
                            fallbackPid = procs[i].Id;
                        }
                    }
                    if (fallbackPid > 0) return fallbackPid;
                }
                finally
                {
                    for (int i = 0; i < procs.Length; i++) procs[i].Dispose();
                }
            }
            throw new InvalidOperationException("未找到可用的 SYSTEM 系统进程。");
        }

        public static TrustedInstallerScope EnterScope()
        {
            return new TrustedInstallerScope();
        }

        /// <summary>
        /// 以 TrustedInstaller 令牌运行无窗口命令行程序（用于受 SYSTEM 保护的计划任务操作）。
        /// </summary>
        public static bool RunProcessAsTrustedInstaller(string exePath, string arguments, int timeoutMs, out int exitCode)
        {
            exitCode = -1;
            EnableProcessPrivilege("SeDebugPrivilege");
            EnableProcessPrivilege("SeImpersonatePrivilege");
            EnableProcessPrivilege("SeAssignPrimaryTokenPrivilege");
            EnableProcessPrivilege("SeIncreaseQuotaPrivilege");

            IntPtr hSystemToken = IntPtr.Zero;
            IntPtr hTiPrimaryToken = IntPtr.Zero;
            bool impersonating = false;

            try
            {
                int tiPid = EnsureTrustedInstallerRunning();
                int sysPid = FindSystemProcessPid();

                hSystemToken = DuplicateTokenFromProcess(sysPid, TokenImpersonation);
                if (!ImpersonateLoggedOnUser(hSystemToken))
                {
                    return false;
                }
                impersonating = true;

                hTiPrimaryToken = DuplicateTokenFromProcess(tiPid, TokenPrimary);
                RevertToSelf();
                impersonating = false;

                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
                si.dwFlags = STARTF_USESHOWWINDOW;
                si.wShowWindow = SW_HIDE;

                PROCESS_INFORMATION pi;
                string cmdLine = "\"" + exePath + "\" " + arguments;
                bool ok = CreateProcessWithTokenW(
                    hTiPrimaryToken,
                    0,
                    null,
                    cmdLine,
                    CREATE_NO_WINDOW,
                    IntPtr.Zero,
                    null,
                    ref si,
                    out pi);

                if (!ok)
                {
                    return false;
                }

                try
                {
                    uint wait = WaitForSingleObject(pi.hProcess, (uint)timeoutMs);
                    if (wait == 0)
                    {
                        uint rawCode;
                        if (GetExitCodeProcess(pi.hProcess, out rawCode))
                        {
                            exitCode = unchecked((int)rawCode);
                            return true;
                        }
                    }
                    return false;
                }
                finally
                {
                    if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                    if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (impersonating) RevertToSelf();
                if (hTiPrimaryToken != IntPtr.Zero) CloseHandle(hTiPrimaryToken);
                if (hSystemToken != IntPtr.Zero) CloseHandle(hSystemToken);
            }
        }

        public sealed class TrustedInstallerScope : IDisposable
        {
            private IntPtr _hTiToken = IntPtr.Zero;
            private bool _disposed;

            public TrustedInstallerScope()
            {
                EnableProcessPrivilege("SeDebugPrivilege");
                EnableProcessPrivilege("SeImpersonatePrivilege");
                EnableProcessPrivilege("SeTakeOwnershipPrivilege");
                EnableProcessPrivilege("SeRestorePrivilege");
                EnableProcessPrivilege("SeBackupPrivilege");

                int tiPid = EnsureTrustedInstallerRunning();
                int sysPid = FindSystemProcessPid();

                IntPtr hSysToken = IntPtr.Zero;
                try
                {
                    hSysToken = DuplicateTokenFromProcess(sysPid, TokenImpersonation);
                    if (!ImpersonateLoggedOnUser(hSysToken))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "无法模拟 SYSTEM 权限。");
                    }

                    _hTiToken = DuplicateTokenFromProcess(tiPid, TokenImpersonation);
                    RevertToSelf();

                    if (!ImpersonateLoggedOnUser(_hTiToken))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "无法模拟 TrustedInstaller 权限。");
                    }

                    EnableThreadPrivilege("SeTakeOwnershipPrivilege");
                    EnableThreadPrivilege("SeRestorePrivilege");
                    EnableThreadPrivilege("SeBackupPrivilege");
                }
                catch
                {
                    RevertToSelf();
                    if (_hTiToken != IntPtr.Zero)
                    {
                        CloseHandle(_hTiToken);
                        _hTiToken = IntPtr.Zero;
                    }
                    throw;
                }
                finally
                {
                    if (hSysToken != IntPtr.Zero) CloseHandle(hSysToken);
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    RevertToSelf();
                    if (_hTiToken != IntPtr.Zero)
                    {
                        CloseHandle(_hTiToken);
                        _hTiToken = IntPtr.Zero;
                    }
                }
            }
        }
    }
}
