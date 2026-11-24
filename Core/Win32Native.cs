using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace JiahaoToolbox.Core
{
    public static class Win32Native
    {
        public const uint TH32CS_SNAPPROCESS = 0x00000002;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public const uint PROCESS_SET_QUOTA = 0x0100;
        public const uint PROCESS_SET_INFORMATION = 0x0200;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public const uint IDLE_PRIORITY_CLASS = 0x00000040;
        public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
        public const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        public const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
        public const uint HIGH_PRIORITY_CLASS = 0x00000080;

        public const int ProcessPowerThrottling = 4;
        public const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        public const int HWND_BROADCAST = 0xFFFF;
        public const int SW_RESTORE = 9;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PROCESSENTRY32W
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetPriorityClass(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, ref PROCESS_POWER_THROTTLING_STATE ProcessInformation, uint ProcessInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessInformation(IntPtr hProcess, int ProcessInformationClass, ref PROCESS_POWER_THROTTLING_STATE ProcessInformation, uint ProcessInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS ppsmemCounters, uint cb);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLengthW(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessageW(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static string FormatWin32Error(int err)
        {
            switch (err)
            {
                case 5: return "权限不足(需管理员或受保护进程, err=5)";
                case 6: return "句柄无效(err=6)";
                case 87: return "进程已退出或参数无效(err=87)";
                case 299: return "受保护进程(err=299)";
                default: return string.Format("Win32错误(err={0})", err);
            }
        }

        public static int GetForegroundPid()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return 0;
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return (int)pid;
        }

        public static void QueryProcessPathAndCreationTime(int pid, out string fullPath, out long creationTime)
        {
            fullPath = "";
            creationTime = 0;
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (h == IntPtr.Zero) return;
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (QueryFullProcessImageNameW(h, 0, sb, ref size))
                {
                    fullPath = sb.ToString();
                }

                long c, ex, k, u;
                if (GetProcessTimes(h, out c, out ex, out k, out u))
                {
                    creationTime = c;
                }
            }
            finally
            {
                CloseHandle(h);
            }
        }

        public static string QueryProcessPath(int pid)
        {
            string p;
            long ct;
            QueryProcessPathAndCreationTime(pid, out p, out ct);
            return p;
        }

        public static double QueryWorkingSetMb(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (h == IntPtr.Zero) return 0.0;
            try
            {
                return GetWorkingSetMbFromHandle(h);
            }
            finally
            {
                CloseHandle(h);
            }
        }

        public static double GetWorkingSetMbFromHandle(IntPtr hProc)
        {
            PROCESS_MEMORY_COUNTERS pmc;
            pmc.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS));
            if (GetProcessMemoryInfo(hProc, out pmc, pmc.cb))
            {
                return pmc.WorkingSetSize.ToUInt64() / (1024.0 * 1024.0);
            }
            return 0.0;
        }

        public static bool QueryProcessSchedulingState(
            int pid,
            out bool hasEcoQuery,
            out uint controlMask,
            out uint stateMask,
            out bool isEco,
            out uint prioRaw,
            out string prioText,
            out long creationTime,
            out string error)
        {
            hasEcoQuery = false;
            controlMask = 0;
            stateMask = 0;
            isEco = false;
            prioRaw = 0;
            prioText = "未知";
            creationTime = 0;
            error = "";

            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (h == IntPtr.Zero)
            {
                error = FormatWin32Error(Marshal.GetLastWin32Error());
                return false;
            }
            try
            {
                long c, ex, k, u;
                if (GetProcessTimes(h, out c, out ex, out k, out u))
                {
                    creationTime = c;
                }

                PROCESS_POWER_THROTTLING_STATE st = new PROCESS_POWER_THROTTLING_STATE();
                st.Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION;
                if (GetProcessInformation(h, ProcessPowerThrottling, ref st, (uint)Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE))))
                {
                    hasEcoQuery = true;
                    controlMask = st.ControlMask;
                    stateMask = st.StateMask;
                    isEco = ((st.ControlMask & PROCESS_POWER_THROTTLING_EXECUTION_SPEED) != 0) &&
                            ((st.StateMask & PROCESS_POWER_THROTTLING_EXECUTION_SPEED) != 0);
                }

                prioRaw = GetPriorityClass(h);
                prioText = PriorityToText(prioRaw);
                return true;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        public static bool QueryEcoModeStatus(int pid, out bool isEco, out uint prioRaw, out string prioText)
        {
            bool hq;
            uint cm, sm;
            long ct;
            string err;
            return QueryProcessSchedulingState(pid, out hq, out cm, out sm, out isEco, out prioRaw, out prioText, out ct, out err);
        }

        public static string PriorityToText(uint prio)
        {
            switch (prio)
            {
                case IDLE_PRIORITY_CLASS: return "低 (Idle)";
                case BELOW_NORMAL_PRIORITY_CLASS: return "低于正常";
                case NORMAL_PRIORITY_CLASS: return "正常";
                case ABOVE_NORMAL_PRIORITY_CLASS: return "高于正常";
                case HIGH_PRIORITY_CLASS: return "高";
                default: return "其他";
            }
        }

        public static bool SetEfficiencyMode(int pid, bool enableEco, uint priorityClass, out string error)
        {
            return ApplyProcessPowerAndPriority(
                pid,
                0,
                true,
                PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                enableEco ? PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0u,
                priorityClass,
                out error);
        }

        public static bool ApplyProcessPowerAndPriority(
            int pid,
            long expectedCreationTime,
            bool setPowerThrottling,
            uint controlMask,
            uint stateMask,
            uint priorityClass,
            out string error)
        {
            error = "";
            uint access = PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION;
            IntPtr h = OpenProcess(access, false, (uint)pid);
            if (h == IntPtr.Zero)
            {
                error = FormatWin32Error(Marshal.GetLastWin32Error());
                return false;
            }
            try
            {
                if (expectedCreationTime > 0)
                {
                    long c, ex, k, u;
                    if (!GetProcessTimes(h, out c, out ex, out k, out u) || c != expectedCreationTime)
                    {
                        error = "进程已重启或PID已复用";
                        return false;
                    }
                }

                bool ecoOk = true;
                int ecoErr = 0;
                if (setPowerThrottling)
                {
                    PROCESS_POWER_THROTTLING_STATE st = new PROCESS_POWER_THROTTLING_STATE();
                    st.Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION;
                    st.ControlMask = controlMask;
                    st.StateMask = stateMask;

                    ecoOk = SetProcessInformation(
                        h,
                        ProcessPowerThrottling,
                        ref st,
                        (uint)Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE)));
                    if (!ecoOk) ecoErr = Marshal.GetLastWin32Error();
                }

                bool prioOk = true;
                int prioErr = 0;
                if (priorityClass != 0)
                {
                    prioOk = SetPriorityClass(h, priorityClass);
                    if (!prioOk) prioErr = Marshal.GetLastWin32Error();
                }

                if (ecoOk && prioOk) return true;
                int code = !ecoOk ? ecoErr : prioErr;
                error = string.Format("{0} [eco={1}, prio={2}]", FormatWin32Error(code), ecoOk, prioOk);
                return false;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        public static bool TrimWorkingSet(int pid, double minWsMb, out double beforeMb, out double afterMb)
        {
            beforeMb = 0;
            afterMb = 0;
            IntPtr h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (h == IntPtr.Zero) return false;
            try
            {
                beforeMb = GetWorkingSetMbFromHandle(h);
                if (beforeMb < minWsMb)
                {
                    afterMb = beforeMb;
                    return false;
                }
                IntPtr negOne = new IntPtr(-1);
                if (!SetProcessWorkingSetSize(h, negOne, negOne))
                {
                    afterMb = beforeMb;
                    return false;
                }
                afterMb = GetWorkingSetMbFromHandle(h);
                return true;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        public static Dictionary<int, string> GetVisibleWindowTitlesByPid()
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;
                int len = GetWindowTextLengthW(hWnd);
                if (len <= 0) return true;
                StringBuilder sb = new StringBuilder(len + 2);
                GetWindowTextW(hWnd, sb, sb.Capacity);
                string title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title)) return true;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                int ipid = (int)pid;
                if (ipid > 0 && !map.ContainsKey(ipid))
                {
                    map[ipid] = title;
                }
                return true;
            }, IntPtr.Zero);
            return map;
        }

        public static string ResolveShortcutTarget(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "";
            if (!filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return filePath;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return filePath;
                object shell = Activator.CreateInstance(shellType);
                try
                {
                    object shortcut = shellType.InvokeMember(
                        "CreateShortcut",
                        System.Reflection.BindingFlags.InvokeMethod,
                        null,
                        shell,
                        new object[] { filePath });
                    if (shortcut != null)
                    {
                        try
                        {
                            object target = shortcut.GetType().InvokeMember(
                                "TargetPath",
                                System.Reflection.BindingFlags.GetProperty,
                                null,
                                shortcut,
                                null);
                            string targetStr = target as string;
                            if (!string.IsNullOrEmpty(targetStr) && File.Exists(targetStr))
                            {
                                return targetStr;
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(shortcut);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch
            {
                // Non-fatal fallback
            }
            return filePath;
        }

        public static string GetFriendlyAppName(string exePath, string fallbackExeName)
        {
            try
            {
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(fvi.FileDescription))
                    {
                        return fvi.FileDescription.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(fvi.ProductName))
                    {
                        return fvi.ProductName.Trim();
                    }
                }
            }
            catch
            {
                // Ignore
            }
            if (!string.IsNullOrEmpty(fallbackExeName))
            {
                return Path.GetFileNameWithoutExtension(fallbackExeName);
            }
            return "未知应用";
        }
    }
}
