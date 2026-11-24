using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace JiahaoToolbox.Core
{
    public sealed class EcoEngine
    {
        private readonly HashSet<int> _appliedPids = new HashSet<int>();
        private readonly Dictionary<int, OriginalProcessState> _managedOriginals = new Dictionary<int, OriginalProcessState>();
        private readonly Dictionary<string, DateTime> _appLastActiveUtc = new Dictionary<string, DateTime>();
        private readonly Dictionary<int, DateTime> _pidLastTrimUtc = new Dictionary<int, DateTime>();

        // Processes we should never adopt as child/sibling targets even if spawned by an app
        private static readonly HashSet<string> ProtectedSystemExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer.exe", "svchost.exe", "csrss.exe", "smss.exe", "wininit.exe", "winlogon.exe",
            "services.exe", "lsass.exe", "dwm.exe", "taskhostw.exe", "sihost.exe", "ctfmon.exe",
            "runtimebroker.exe", "applicationframehost.exe", "shellexperiencehost.exe",
            "startmenuexperiencehost.exe", "searchhost.exe", "textinputhost.exe", "systemsettings.exe",
            "conhost.exe", "cmd.exe", "powershell.exe", "pwsh.exe", "msiexec.exe", "rundll32.exe",
            "regsvr32.exe", "dllhost.exe", "wmiprvse.exe", "taskmgr.exe", "autoecocontrol.exe", "jiahaotoolbox.exe",
            "openconsole.exe", "windowsterminal.exe"
        };

        public int ManagedProcessCount
        {
            get { return _managedOriginals.Count; }
        }

        public bool IsPidManaged(int pid)
        {
            return _managedOriginals.ContainsKey(pid);
        }

        public void ResetAppliedCache()
        {
            _appliedPids.Clear();
        }

        public static uint ResolvePriorityFlag(string name)
        {
            if (string.Equals(name, "idle", StringComparison.OrdinalIgnoreCase))
            {
                return Win32Native.IDLE_PRIORITY_CLASS;
            }
            if (string.Equals(name, "normal", StringComparison.OrdinalIgnoreCase))
            {
                return Win32Native.NORMAL_PRIORITY_CLASS;
            }
            return Win32Native.BELOW_NORMAL_PRIORITY_CLASS;
        }

        public static bool IsSafeInstallDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            string norm = NormalizeDir(dir);
            if (norm.Length <= 4) return false; // e.g. C:\

            string winDir = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            string sysDir = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.System));
            string pf = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            string pf86 = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            string localApp = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            string roamingApp = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            string userProfile = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            string desktop = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            string localPrograms = NormalizeDir(Path.Combine(localApp, "Programs"));

            if (norm.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) return false;
            if (norm.StartsWith(sysDir, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(norm, pf, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(norm, pf86, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(norm, localApp, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(norm, roamingApp, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(norm, userProfile, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(norm, desktop, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(norm, localPrograms, StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        public static bool IsWithinInstallDir(string fullPath, string normInstallDir)
        {
            if (string.IsNullOrEmpty(normInstallDir) || string.IsNullOrEmpty(fullPath)) return false;
            return fullPath.StartsWith(normInstallDir, StringComparison.OrdinalIgnoreCase);
        }

        public static string DeriveAppRootDir(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath)) return "";
            try
            {
                string dir = Path.GetDirectoryName(exePath);
                if (string.IsNullOrWhiteSpace(dir)) return "";

                string folderName = Path.GetFileName(dir.TrimEnd('\\'));
                if (!string.IsNullOrEmpty(folderName))
                {
                    bool looksVersioned =
                        folderName.StartsWith("app-", StringComparison.OrdinalIgnoreCase) ||
                        folderName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                        folderName.Equals("current", StringComparison.OrdinalIgnoreCase) ||
                        (char.IsDigit(folderName[0]) && folderName.Contains("."));
                    if (looksVersioned)
                    {
                        string parent = Path.GetDirectoryName(dir.TrimEnd('\\'));
                        if (!string.IsNullOrEmpty(parent) && IsSafeInstallDirectory(parent))
                        {
                            return NormalizeDir(parent);
                        }
                    }
                }

                return IsSafeInstallDirectory(dir) ? NormalizeDir(dir) : "";
            }
            catch
            {
                return "";
            }
        }

        public static string NormalizeDir(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return "";
            string trimmed = dir.Trim().Replace('/', '\\').TrimEnd('\\');
            return trimmed + "\\";
        }

        public List<ProcessSnapshotItem> TakeSnapshot(bool includeMemory)
        {
            List<ProcessSnapshotItem> list = new List<ProcessSnapshotItem>();
            IntPtr snap = Win32Native.CreateToolhelp32Snapshot(Win32Native.TH32CS_SNAPPROCESS, 0);
            if (snap == Win32Native.INVALID_HANDLE_VALUE) return list;

            int myPid = Process.GetCurrentProcess().Id;
            Dictionary<int, long> aliveCreateTimes = new Dictionary<int, long>();

            try
            {
                Win32Native.PROCESSENTRY32W entry = new Win32Native.PROCESSENTRY32W();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(Win32Native.PROCESSENTRY32W));
                if (Win32Native.Process32FirstW(snap, ref entry))
                {
                    do
                    {
                        int pid = (int)entry.th32ProcessID;
                        if (pid <= 4 || pid == myPid) continue;

                        string exe = entry.szExeFile ?? "";
                        string exeLower = exe.ToLowerInvariant();
                        string fullPath;
                        long createTime;
                        Win32Native.QueryProcessPathAndCreationTime(pid, out fullPath, out createTime);
                        aliveCreateTimes[pid] = createTime;

                        ProcessSnapshotItem item = new ProcessSnapshotItem();
                        item.Pid = pid;
                        item.ParentPid = (int)entry.th32ParentProcessID;
                        item.CreationTime = createTime;
                        item.ExeName = exe;
                        item.ExeNameLower = exeLower;
                        item.FullPath = fullPath;
                        item.WorkingSetMb = includeMemory ? Win32Native.QueryWorkingSetMb(pid) : 0.0;
                        list.Add(item);
                    }
                    while (Win32Native.Process32NextW(snap, ref entry));
                }
            }
            finally
            {
                Win32Native.CloseHandle(snap);
            }

            // Prune exited or PID-reused entries from tracking state
            List<int> staleManaged = new List<int>();
            foreach (KeyValuePair<int, OriginalProcessState> kv in _managedOriginals)
            {
                long liveCt;
                if (!aliveCreateTimes.TryGetValue(kv.Key, out liveCt) ||
                    (kv.Value.CreationTime > 0 && liveCt > 0 && liveCt != kv.Value.CreationTime))
                {
                    staleManaged.Add(kv.Key);
                }
            }
            for (int i = 0; i < staleManaged.Count; i++)
            {
                _managedOriginals.Remove(staleManaged[i]);
                _appliedPids.Remove(staleManaged[i]);
                _pidLastTrimUtc.Remove(staleManaged[i]);
            }

            List<int> staleApplied = new List<int>();
            foreach (int pid in _appliedPids)
            {
                if (!aliveCreateTimes.ContainsKey(pid)) staleApplied.Add(pid);
            }
            for (int i = 0; i < staleApplied.Count; i++)
            {
                _appliedPids.Remove(staleApplied[i]);
                _pidLastTrimUtc.Remove(staleApplied[i]);
            }

            return list;
        }

        private static Dictionary<int, List<ProcessSnapshotItem>> BuildChildrenMap(List<ProcessSnapshotItem> procs)
        {
            Dictionary<int, List<ProcessSnapshotItem>> map = new Dictionary<int, List<ProcessSnapshotItem>>();
            for (int i = 0; i < procs.Count; i++)
            {
                ProcessSnapshotItem p = procs[i];
                List<ProcessSnapshotItem> kids;
                if (!map.TryGetValue(p.ParentPid, out kids))
                {
                    kids = new List<ProcessSnapshotItem>();
                    map[p.ParentPid] = kids;
                }
                kids.Add(p);
            }
            return map;
        }

        private static bool IsVerifiedChild(ProcessSnapshotItem parent, ProcessSnapshotItem child)
        {
            if (parent == null || child == null) return false;
            if (child.ParentPid != parent.Pid) return false;
            // Creation time check prevents false parent-child links when Windows reuses an exited parent's PID
            if (parent.CreationTime > 0 && child.CreationTime > 0 && child.CreationTime < parent.CreationTime)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Match all processes belonging to each configured TargetAppEntry safely:
        /// 1. When InstallDir is known: ONLY match processes inside InstallDir (eliminates cross-directory same-name collisions!).
        /// 2. Verified parent->child process tree expansion (checks ParentPid + CreationTime):
        ///    - Children inside InstallDir may be auto-learned into KnownExes.
        ///    - Cross-directory children are matched ONLY temporarily for that verified live PID and NEVER persisted into KnownExes.
        /// </summary>
        public Dictionary<string, List<MatchedProcessInfo>> MatchAllApps(
            ToolboxConfig config,
            List<ProcessSnapshotItem> procs,
            bool queryEcoStatus,
            out bool configModified,
            List<string> discoveryLogs)
        {
            configModified = false;
            Dictionary<string, List<MatchedProcessInfo>> result = new Dictionary<string, List<MatchedProcessInfo>>();
            Dictionary<int, List<ProcessSnapshotItem>> childrenMap = BuildChildrenMap(procs);
            HashSet<int> claimedPids = new HashSet<int>();

            foreach (TargetAppEntry app in config.Apps)
            {
                List<MatchedProcessInfo> matchedList = new List<MatchedProcessInfo>();
                result[app.Id] = matchedList;

                HashSet<string> knownLower = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(app.PrimaryExe)) knownLower.Add(app.PrimaryExe);
                if (app.KnownExes != null)
                {
                    for (int i = 0; i < app.KnownExes.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(app.KnownExes[i])) knownLower.Add(app.KnownExes[i]);
                    }
                }

                string normInstallDir = IsSafeInstallDirectory(app.InstallDir) ? NormalizeDir(app.InstallDir) : "";

                // Pre-learn install directory from PrimaryExe (or preset KnownExes) if InstallDir is still empty
                if (string.IsNullOrEmpty(normInstallDir))
                {
                    for (int i = 0; i < procs.Count; i++)
                    {
                        ProcessSnapshotItem p = procs[i];
                        if (claimedPids.Contains(p.Pid)) continue;
                        if (ProtectedSystemExes.Contains(p.ExeNameLower)) continue;
                        if (string.IsNullOrEmpty(p.FullPath)) continue;

                        bool isSeed =
                            string.Equals(p.ExeName, app.PrimaryExe, StringComparison.OrdinalIgnoreCase) ||
                            knownLower.Contains(p.ExeName);
                        if (isSeed)
                        {
                            string derived = DeriveAppRootDir(p.FullPath);
                            if (!string.IsNullOrEmpty(derived))
                            {
                                app.InstallDir = derived;
                                normInstallDir = derived;
                                configModified = true;
                                if (discoveryLogs != null)
                                {
                                    discoveryLogs.Add(string.Format("[自动学习目录] {0} -> {1}", app.DisplayName, derived));
                                }
                                break;
                            }
                        }
                    }
                }

                Queue<ProcessSnapshotItem> bfs = new Queue<ProcessSnapshotItem>();
                Dictionary<int, string> localMatchedSource = new Dictionary<int, string>();

                // Pass 1: Seed matching — strictly path-scoped whenever InstallDir is known
                for (int i = 0; i < procs.Count; i++)
                {
                    ProcessSnapshotItem p = procs[i];
                    if (claimedPids.Contains(p.Pid)) continue;
                    if (ProtectedSystemExes.Contains(p.ExeNameLower)) continue;

                    string source = null;
                    if (!string.IsNullOrEmpty(normInstallDir))
                    {
                        if (!string.IsNullOrEmpty(p.FullPath))
                        {
                            // Path is readable: MUST reside within app's trusted install directory
                            if (IsWithinInstallDir(p.FullPath, normInstallDir))
                            {
                                source = knownLower.Contains(p.ExeName) ? "主程序/同目录" : "同目录衍生";
                            }
                        }
                        else if (string.Equals(p.ExeName, app.PrimaryExe, StringComparison.OrdinalIgnoreCase))
                        {
                            // Unreadable path (likely elevated/protected): only allow exact PrimaryExe so UI reports access-denied transparently
                            source = "主程序(路径受限)";
                        }
                    }
                    else
                    {
                        // InstallDir still unknown (e.g. preset not yet observed with a safe path)
                        if (knownLower.Contains(p.ExeName))
                        {
                            source = "主程序/预设名";
                        }
                    }

                    if (source != null)
                    {
                        localMatchedSource[p.Pid] = source;
                        bfs.Enqueue(p);
                    }
                }

                // Pass 2: Verified recursive parent->child expansion
                while (bfs.Count > 0)
                {
                    ProcessSnapshotItem cur = bfs.Dequeue();
                    List<ProcessSnapshotItem> kids;
                    if (!childrenMap.TryGetValue(cur.Pid, out kids)) continue;

                    for (int i = 0; i < kids.Count; i++)
                    {
                        ProcessSnapshotItem child = kids[i];
                        if (localMatchedSource.ContainsKey(child.Pid) || claimedPids.Contains(child.Pid)) continue;
                        if (ProtectedSystemExes.Contains(child.ExeNameLower)) continue;
                        if (!IsVerifiedChild(cur, child)) continue;

                        bool inInstallDir = IsWithinInstallDir(child.FullPath, normInstallDir);
                        localMatchedSource[child.Pid] = inInstallDir ? "同目录子进程" : "父子进程树(临时)";
                        bfs.Enqueue(child);
                    }
                }

                // Collect matches; ONLY persist auto-learned exe names that live inside the trusted InstallDir!
                for (int i = 0; i < procs.Count; i++)
                {
                    ProcessSnapshotItem p = procs[i];
                    string src;
                    if (!localMatchedSource.TryGetValue(p.Pid, out src)) continue;
                    claimedPids.Add(p.Pid);

                    if (!string.IsNullOrEmpty(normInstallDir) &&
                        IsWithinInstallDir(p.FullPath, normInstallDir) &&
                        !ToolboxConfig.ContainsIgnoreCase(app.KnownExes, p.ExeName))
                    {
                        app.KnownExes.Add(p.ExeName);
                        configModified = true;
                        if (discoveryLogs != null)
                        {
                            discoveryLogs.Add(string.Format("[同目录学习] {0} -> {1} ({2})", app.DisplayName, p.ExeName, src));
                        }
                    }

                    bool isEco = false;
                    uint prioRaw = 0;
                    string prioText = "";
                    if (queryEcoStatus)
                    {
                        Win32Native.QueryEcoModeStatus(p.Pid, out isEco, out prioRaw, out prioText);
                    }

                    MatchedProcessInfo info = new MatchedProcessInfo();
                    info.Pid = p.Pid;
                    info.ParentPid = p.ParentPid;
                    info.CreationTime = p.CreationTime;
                    info.ExeName = p.ExeName;
                    info.FullPath = p.FullPath;
                    info.AppId = app.Id;
                    info.AppDisplayName = app.DisplayName;
                    info.MatchSource = src;
                    info.IsEcoEnabled = isEco;
                    info.PriorityRaw = prioRaw;
                    info.PriorityText = prioText;
                    info.WorkingSetMb = p.WorkingSetMb;
                    info.IsManagedByTool = _managedOriginals.ContainsKey(p.Pid);
                    info.LastActionText = "";
                    matchedList.Add(info);
                }
            }

            return result;
        }

        private string ResolveForegroundAppId(
            int fgPid,
            List<ProcessSnapshotItem> procs,
            Dictionary<string, List<MatchedProcessInfo>> matchedByApp)
        {
            if (fgPid <= 4) return null;

            Dictionary<int, string> pidToApp = new Dictionary<int, string>();
            foreach (KeyValuePair<string, List<MatchedProcessInfo>> kv in matchedByApp)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    pidToApp[kv.Value[i].Pid] = kv.Key;
                }
            }

            string direct;
            if (pidToApp.TryGetValue(fgPid, out direct)) return direct;

            // Walk parent chain up to 6 hops in case the foreground window belongs to a helper dialog/child
            Dictionary<int, ProcessSnapshotItem> byPid = new Dictionary<int, ProcessSnapshotItem>();
            for (int i = 0; i < procs.Count; i++) byPid[procs[i].Pid] = procs[i];

            int curPid = fgPid;
            for (int hop = 0; hop < 6; hop++)
            {
                ProcessSnapshotItem cur;
                if (!byPid.TryGetValue(curPid, out cur)) break;
                int parentPid = cur.ParentPid;
                if (parentPid <= 4 || parentPid == curPid) break;

                ProcessSnapshotItem parent;
                if (byPid.TryGetValue(parentPid, out parent) && !IsVerifiedChild(parent, cur)) break;

                string appId;
                if (pidToApp.TryGetValue(parentPid, out appId)) return appId;
                curPid = parentPid;
            }

            return null;
        }

        private bool TryRestoreManagedPid(int pid, out bool skippedExitedOrReused, out string error)
        {
            skippedExitedOrReused = false;
            error = "";
            OriginalProcessState orig;
            if (!_managedOriginals.TryGetValue(pid, out orig))
            {
                return false;
            }

            bool ok = Win32Native.ApplyProcessPowerAndPriority(
                pid,
                orig.CreationTime,
                orig.HasOriginalEcoQuery,
                orig.OriginalControlMask,
                orig.OriginalStateMask,
                orig.OriginalPriorityClass,
                out error);

            if (ok)
            {
                _managedOriginals.Remove(pid);
                _appliedPids.Remove(pid);
                return true;
            }

            if (error.IndexOf("已退出", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("已复用", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                skippedExitedOrReused = true;
                _managedOriginals.Remove(pid);
                _appliedPids.Remove(pid);
            }
            return false;
        }

        public WatcherPassResult RunPass(
            ToolboxConfig config,
            bool applyActions,
            bool forceTrimNow,
            string onlyAppId = null,
            bool forceTargetApp = false)
        {
            WatcherPassResult res = new WatcherPassResult();
            DateTime now = DateTime.UtcNow;
            List<ProcessSnapshotItem> procs = TakeSnapshot(true);
            bool cfgChanged;
            Dictionary<string, List<MatchedProcessInfo>> matchedByApp = MatchAllApps(
                config, procs, true, out cfgChanged, res.LogLines);
            res.ConfigModified = cfgChanged;

            int fgPid = Win32Native.GetForegroundPid();
            string fgAppId = ResolveForegroundAppId(fgPid, procs, matchedByApp);
            res.ForegroundAppId = fgAppId;
            if (fgAppId != null)
            {
                _appLastActiveUtc[fgAppId] = now;
            }

            uint prioFlag = ResolvePriorityFlag(config.PriorityClass);
            List<string> newlyAppliedNames = new List<string>();
            List<string> fgRestoredLogs = new List<string>();
            List<string> trimmedDetails = new List<string>();

            foreach (TargetAppEntry app in config.Apps)
            {
                List<MatchedProcessInfo> list;
                if (!matchedByApp.TryGetValue(app.Id, out list))
                {
                    list = new List<MatchedProcessInfo>();
                }

                bool isScopedMatch = string.IsNullOrEmpty(onlyAppId) ||
                    string.Equals(app.Id, onlyAppId, StringComparison.OrdinalIgnoreCase);
                bool shouldApplyThisApp = applyActions && isScopedMatch && (app.Enabled || (forceTargetApp && !string.IsNullOrEmpty(onlyAppId)));
                bool isFgExempt = config.EnableForegroundExemption &&
                    !string.IsNullOrEmpty(fgAppId) &&
                    string.Equals(app.Id, fgAppId, StringComparison.OrdinalIgnoreCase);

                AppRuntimeSummary summary = new AppRuntimeSummary();
                summary.AppId = app.Id;
                summary.RunningCount = list.Count;
                summary.IsForegroundExempt = isFgExempt;
                summary.Processes = list;

                if (app.Enabled && list.Count > 0 && !_appLastActiveUtc.ContainsKey(app.Id))
                {
                    _appLastActiveUtc[app.Id] = now;
                }

                int fgRestoredForApp = 0;

                for (int i = 0; i < list.Count; i++)
                {
                    MatchedProcessInfo mp = list[i];
                    summary.TotalWorkingSetMb += mp.WorkingSetMb;

                    if (shouldApplyThisApp)
                    {
                        if (isFgExempt)
                        {
                            // Foreground Exemption: restore any process this tool previously throttled and skip applying
                            if (_managedOriginals.ContainsKey(mp.Pid))
                            {
                                bool exited;
                                string rErr;
                                if (TryRestoreManagedPid(mp.Pid, out exited, out rErr))
                                {
                                    fgRestoredForApp++;
                                    res.ForegroundRestoredCount++;
                                    bool liveEco;
                                    uint livePrio;
                                    string livePrioText;
                                    if (Win32Native.QueryEcoModeStatus(mp.Pid, out liveEco, out livePrio, out livePrioText))
                                    {
                                        mp.IsEcoEnabled = liveEco;
                                        mp.PriorityRaw = livePrio;
                                        mp.PriorityText = livePrioText;
                                    }
                                    mp.IsManagedByTool = false;
                                }
                            }
                            res.SkippedForegroundCount++;
                            mp.LastActionText = "跳过(前台豁免)";
                        }
                        else
                        {
                            bool needEco = config.EnableEfficiencyMode && !mp.IsEcoEnabled;
                            bool needPrio = prioFlag != 0 && mp.PriorityRaw != prioFlag;
                            if (!needEco && !needPrio)
                            {
                                res.SkippedAlreadyActiveCount++;
                                mp.LastActionText = "跳过(已是目标态)";
                            }
                            else
                            {
                                // Capture original scheduling state BEFORE our first modification of this PID
                                OriginalProcessState origCandidate = null;
                                if (!_managedOriginals.ContainsKey(mp.Pid))
                                {
                                    bool hq;
                                    uint cm, sm, curPrio;
                                    bool curEco;
                                    string curPrioText, qErr;
                                    long curCt;
                                    if (Win32Native.QueryProcessSchedulingState(
                                        mp.Pid, out hq, out cm, out sm, out curEco, out curPrio, out curPrioText, out curCt, out qErr))
                                    {
                                        origCandidate = new OriginalProcessState
                                        {
                                            Pid = mp.Pid,
                                            CreationTime = curCt > 0 ? curCt : mp.CreationTime,
                                            AppId = app.Id,
                                            ExeName = mp.ExeName,
                                            HasOriginalEcoQuery = hq,
                                            OriginalControlMask = cm,
                                            OriginalStateMask = sm,
                                            OriginalPriorityClass = curPrio
                                        };
                                    }
                                }

                                string err;
                                if (Win32Native.ApplyProcessPowerAndPriority(
                                    mp.Pid,
                                    mp.CreationTime,
                                    true,
                                    Win32Native.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                                    config.EnableEfficiencyMode ? Win32Native.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0u,
                                    prioFlag,
                                    out err))
                                {
                                    if (origCandidate != null && !_managedOriginals.ContainsKey(mp.Pid))
                                    {
                                        _managedOriginals[mp.Pid] = origCandidate;
                                    }
                                    _appliedPids.Add(mp.Pid);
                                    mp.IsEcoEnabled = config.EnableEfficiencyMode;
                                    mp.PriorityRaw = prioFlag;
                                    mp.PriorityText = Win32Native.PriorityToText(prioFlag);
                                    mp.IsManagedByTool = _managedOriginals.ContainsKey(mp.Pid);
                                    mp.LastActionText = "成功";
                                    res.NewlyAppliedCount++;
                                    newlyAppliedNames.Add(string.Format("{0}:{1}({2})", app.DisplayName, mp.ExeName, mp.Pid));
                                }
                                else
                                {
                                    res.FailedCount++;
                                    mp.LastActionText = "失败: " + err;
                                    res.FailureDetails.Add(string.Format("{0}:{1}({2}) -> {3}", app.DisplayName, mp.ExeName, mp.Pid, err));
                                }
                            }

                            if (config.EnableIdleMemoryTrim && app.Id != fgAppId)
                            {
                                DateTime lastActive;
                                if (!_appLastActiveUtc.TryGetValue(app.Id, out lastActive)) lastActive = now;
                                double bgElapsed = (now - lastActive).TotalSeconds;

                                DateTime lastTrim;
                                bool hasTrimmed = _pidLastTrimUtc.TryGetValue(mp.Pid, out lastTrim);
                                double sinceTrim = hasTrimmed ? (now - lastTrim).TotalSeconds : 1e9;

                                bool ready = forceTrimNow || (bgElapsed >= config.IdleTrimDelaySeconds && sinceTrim >= config.TrimCooldownSeconds);
                                if (ready)
                                {
                                    double beforeMb, afterMb;
                                    if (Win32Native.TrimWorkingSet(mp.Pid, config.TrimMinWorkingSetMb, out beforeMb, out afterMb))
                                    {
                                        _pidLastTrimUtc[mp.Pid] = now;
                                        double saved = Math.Max(0.0, beforeMb - afterMb);
                                        mp.WorkingSetMb = afterMb;
                                        if (saved >= 5.0)
                                        {
                                            res.TotalTrimmedMb += saved;
                                            trimmedDetails.Add(string.Format("{0}({1}): {2:F0}->{3:F0}MB", mp.ExeName, mp.Pid, beforeMb, afterMb));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (mp.IsEcoEnabled)
                    {
                        summary.EcoActiveCount++;
                    }
                }

                if (fgRestoredForApp > 0)
                {
                    fgRestoredLogs.Add(string.Format("{0} ({1}个进程)", app.DisplayName, fgRestoredForApp));
                }

                res.PerApp[app.Id] = summary;
                if (app.Enabled)
                {
                    res.TotalTrackedProcesses += summary.RunningCount;
                    res.TotalEcoActiveProcesses += summary.EcoActiveCount;
                }
            }

            if (fgRestoredLogs.Count > 0)
            {
                res.LogLines.Add(string.Format("[前台豁免] 检测到应用切至前台，已临时还原原状态：{0}", string.Join(", ", fgRestoredLogs.ToArray())));
            }
            if (newlyAppliedNames.Count > 0)
            {
                res.LogLines.Add(string.Format("已开启效率模式 ({0}): {1}", newlyAppliedNames.Count, string.Join(", ", newlyAppliedNames.ToArray())));
            }
            if (res.FailureDetails.Count > 0)
            {
                int showCount = Math.Min(res.FailureDetails.Count, 4);
                List<string> head = res.FailureDetails.GetRange(0, showCount);
                string suffix = res.FailureDetails.Count > showCount ? string.Format(" 等共 {0} 项", res.FailureDetails.Count) : "";
                res.LogLines.Add(string.Format("部分进程设置失败 ({0}): {1}{2}", res.FailedCount, string.Join(" ; ", head.ToArray()), suffix));
            }
            if (trimmedDetails.Count > 0)
            {
                res.LogLines.Add(string.Format("后台缩减工作集 (-{0:F0}MB): {1}", res.TotalTrimmedMb, string.Join(" | ", trimmedDetails.ToArray())));
            }

            return res;
        }

        /// <summary>
        /// Accurately restore ONLY processes previously modified by AutoEcoControl back to their
        /// exact original PriorityClass & PowerThrottling state. Unmodified processes are never overwritten.
        /// </summary>
        public RestoreResult RestoreNormalMode(ToolboxConfig config, string onlyAppId, List<string> logs)
        {
            RestoreResult rr = new RestoreResult();
            List<ProcessSnapshotItem> procs = TakeSnapshot(false);
            bool dummy;
            Dictionary<string, List<MatchedProcessInfo>> matchedByApp = MatchAllApps(config, procs, false, out dummy, null);

            // Collect managed PIDs matching scope
            List<int> pidsToRestore = new List<int>();
            foreach (KeyValuePair<int, OriginalProcessState> kv in _managedOriginals)
            {
                if (string.IsNullOrEmpty(onlyAppId) ||
                    string.Equals(kv.Value.AppId, onlyAppId, StringComparison.OrdinalIgnoreCase))
                {
                    pidsToRestore.Add(kv.Key);
                }
            }

            // Count currently running matched processes that were NOT modified by this tool
            foreach (TargetAppEntry app in config.Apps)
            {
                if (!string.IsNullOrEmpty(onlyAppId) && !string.Equals(app.Id, onlyAppId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                List<MatchedProcessInfo> list;
                if (!matchedByApp.TryGetValue(app.Id, out list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!_managedOriginals.ContainsKey(list[i].Pid))
                    {
                        rr.SkippedUnmanagedCount++;
                    }
                }
            }

            for (int i = 0; i < pidsToRestore.Count; i++)
            {
                int pid = pidsToRestore[i];
                OriginalProcessState orig;
                string exeName = _managedOriginals.TryGetValue(pid, out orig) ? orig.ExeName : "";
                bool exited;
                string err;
                if (TryRestoreManagedPid(pid, out exited, out err))
                {
                    rr.RestoredCount++;
                }
                else if (exited)
                {
                    rr.SkippedExitedOrReusedCount++;
                }
                else
                {
                    rr.FailedCount++;
                    rr.FailureDetails.Add(string.Format("{0}({1}) -> {2}", exeName, pid, err));
                }
            }

            if (logs != null)
            {
                string scope = string.IsNullOrEmpty(onlyAppId) ? "全部受控应用" : "当前应用";
                logs.Add(string.Format("精准恢复完成（{0}）：还原原状态 {1} 个 · 跳过 {2} 个（非本工具修改）· 失败 {3} 个",
                    scope, rr.RestoredCount, rr.SkippedUnmanagedCount, rr.FailedCount));
                if (rr.FailureDetails.Count > 0)
                {
                    logs.Add("恢复失败原因: " + string.Join(" ; ", rr.FailureDetails.ToArray()));
                }
            }
            return rr;
        }

        /// <summary>
        /// Scan currently running user applications and preview all same-directory & verified child processes.
        /// Only executables residing inside InstallDir (plus PrimaryExe) are placed into DetectedExes.
        /// </summary>
        public List<RunningAppCandidate> ScanRunningApps()
        {
            List<ProcessSnapshotItem> procs = TakeSnapshot(true);
            Dictionary<int, string> titles = Win32Native.GetVisibleWindowTitlesByPid();
            Dictionary<int, List<ProcessSnapshotItem>> childrenMap = BuildChildrenMap(procs);
            Dictionary<int, ProcessSnapshotItem> byPid = new Dictionary<int, ProcessSnapshotItem>();
            for (int i = 0; i < procs.Count; i++) byPid[procs[i].Pid] = procs[i];

            string winDir = NormalizeDir(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

            List<ProcessSnapshotItem> seeds = new List<ProcessSnapshotItem>();
            HashSet<int> seedPids = new HashSet<int>();
            foreach (KeyValuePair<int, string> kv in titles)
            {
                ProcessSnapshotItem p;
                if (byPid.TryGetValue(kv.Key, out p) && !ProtectedSystemExes.Contains(p.ExeNameLower) && !string.IsNullOrEmpty(p.FullPath))
                {
                    if (seedPids.Add(p.Pid)) seeds.Add(p);
                }
            }
            for (int i = 0; i < procs.Count; i++)
            {
                ProcessSnapshotItem p = procs[i];
                if (seedPids.Contains(p.Pid)) continue;
                if (ProtectedSystemExes.Contains(p.ExeNameLower)) continue;
                if (string.IsNullOrEmpty(p.FullPath)) continue;
                if (p.FullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) continue;
                if (seedPids.Add(p.Pid)) seeds.Add(p);
            }

            seeds.Sort(delegate(ProcessSnapshotItem a, ProcessSnapshotItem b)
            {
                bool aw = titles.ContainsKey(a.Pid);
                bool bw = titles.ContainsKey(b.Pid);
                if (aw != bw) return bw.CompareTo(aw);
                int da = (a.FullPath ?? "").Split('\\').Length;
                int db = (b.FullPath ?? "").Split('\\').Length;
                return da.CompareTo(db);
            });

            string commonShared = NormalizeDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "microsoft shared"));
            Dictionary<string, RunningAppCandidate> grouped = new Dictionary<string, RunningAppCandidate>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> alreadyGroupedPids = new HashSet<int>();

            for (int sIdx = 0; sIdx < seeds.Count; sIdx++)
            {
                ProcessSnapshotItem p = seeds[sIdx];
                if (alreadyGroupedPids.Contains(p.Pid)) continue;
                if (!string.IsNullOrEmpty(p.FullPath) && p.FullPath.StartsWith(commonShared, StringComparison.OrdinalIgnoreCase)) continue;

                string installDir = DeriveAppRootDir(p.FullPath);
                string groupKey = !string.IsNullOrEmpty(installDir) ? installDir : (p.FullPath ?? p.ExeNameLower);
                if (grouped.ContainsKey(groupKey)) continue;

                HashSet<int> visited = new HashSet<int>();
                Queue<ProcessSnapshotItem> q = new Queue<ProcessSnapshotItem>();
                for (int i = 0; i < procs.Count; i++)
                {
                    ProcessSnapshotItem x = procs[i];
                    if (alreadyGroupedPids.Contains(x.Pid)) continue;
                    if (ProtectedSystemExes.Contains(x.ExeNameLower)) continue;
                    bool sameApp = !string.IsNullOrEmpty(installDir)
                        ? IsWithinInstallDir(x.FullPath, installDir)
                        : string.Equals(x.FullPath, p.FullPath, StringComparison.OrdinalIgnoreCase);
                    if (sameApp && visited.Add(x.Pid))
                    {
                        q.Enqueue(x);
                    }
                }
                while (q.Count > 0)
                {
                    ProcessSnapshotItem cur = q.Dequeue();
                    List<ProcessSnapshotItem> kids;
                    if (!childrenMap.TryGetValue(cur.Pid, out kids)) continue;
                    for (int i = 0; i < kids.Count; i++)
                    {
                        if (!alreadyGroupedPids.Contains(kids[i].Pid) &&
                            !ProtectedSystemExes.Contains(kids[i].ExeNameLower) &&
                            IsVerifiedChild(cur, kids[i]) &&
                            visited.Add(kids[i].Pid))
                        {
                            q.Enqueue(kids[i]);
                        }
                    }
                }
                foreach (int pid in visited) alreadyGroupedPids.Add(pid);

                ProcessSnapshotItem bestPrimary = p;
                string bestTitle = "";
                if (titles.ContainsKey(p.Pid)) bestTitle = titles[p.Pid];

                List<string> trustedExes = new List<string>();
                List<string> previewDetails = new List<string>();
                double totalMb = 0;
                foreach (int pid in visited)
                {
                    ProcessSnapshotItem mp;
                    if (byPid.TryGetValue(pid, out mp))
                    {
                        totalMb += mp.WorkingSetMb;
                        bool inTrustedDir = string.IsNullOrEmpty(installDir)
                            ? string.Equals(mp.FullPath, p.FullPath, StringComparison.OrdinalIgnoreCase)
                            : IsWithinInstallDir(mp.FullPath, installDir);
                        if (inTrustedDir && !ToolboxConfig.ContainsIgnoreCase(trustedExes, mp.ExeName))
                        {
                            trustedExes.Add(mp.ExeName);
                        }
                        string tag = inTrustedDir ? "同目录" : "临时子进程";
                        previewDetails.Add(string.Format("{0} (PID {1}, {2}) -> {3}",
                            mp.ExeName, mp.Pid, tag, string.IsNullOrEmpty(mp.FullPath) ? "路径受限" : mp.FullPath));

                        if (string.IsNullOrEmpty(bestTitle) && titles.ContainsKey(pid))
                        {
                            bestTitle = titles[pid];
                            bestPrimary = mp;
                        }
                        else if (string.IsNullOrEmpty(bestTitle) && mp.WorkingSetMb > bestPrimary.WorkingSetMb)
                        {
                            bestPrimary = mp;
                        }
                    }
                }
                if (!ToolboxConfig.ContainsIgnoreCase(trustedExes, bestPrimary.ExeName))
                {
                    trustedExes.Insert(0, bestPrimary.ExeName);
                }

                RunningAppCandidate cand = new RunningAppCandidate();
                cand.DisplayName = Win32Native.GetFriendlyAppName(bestPrimary.FullPath, bestPrimary.ExeName);
                cand.WindowTitle = bestTitle;
                cand.PrimaryExe = bestPrimary.ExeName;
                cand.FullPath = bestPrimary.FullPath;
                cand.InstallDir = installDir;
                cand.DetectedExes = trustedExes;
                cand.PreviewProcessDetails = previewDetails;
                cand.RunningProcessCount = visited.Count;
                cand.TotalMemoryMb = totalMb;
                grouped[groupKey] = cand;
            }

            List<RunningAppCandidate> list = new List<RunningAppCandidate>(grouped.Values);
            list.Sort(delegate(RunningAppCandidate a, RunningAppCandidate b)
            {
                bool aHasWin = !string.IsNullOrEmpty(a.WindowTitle);
                bool bHasWin = !string.IsNullOrEmpty(b.WindowTitle);
                if (aHasWin != bHasWin) return bHasWin.CompareTo(aHasWin);
                return b.TotalMemoryMb.CompareTo(a.TotalMemoryMb);
            });
            return list;
        }
    }
}
