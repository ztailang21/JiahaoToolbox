using System;
using System.Collections.Generic;

namespace JiahaoToolbox.Core
{
    public enum StatusTone
    {
        Gray,
        Green,
        Amber,
        Red
    }

    public delegate void LogCallback(string level, string message);

    // ========================
    // 1. Eco Mode Models
    // ========================

    public sealed class TargetAppEntry
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string PrimaryExe { get; set; }
        public string InstallDir { get; set; }
        public List<string> KnownExes { get; set; }
        public bool Enabled { get; set; }

        public TargetAppEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            DisplayName = "";
            PrimaryExe = "";
            InstallDir = "";
            KnownExes = new List<string>();
            Enabled = false;
        }
    }

    public sealed class ProcessSnapshotItem
    {
        public int Pid { get; set; }
        public int ParentPid { get; set; }
        public long CreationTime { get; set; }
        public string ExeName { get; set; }
        public string ExeNameLower { get; set; }
        public string FullPath { get; set; }
        public double WorkingSetMb { get; set; }
    }

    public sealed class OriginalProcessState
    {
        public int Pid { get; set; }
        public long CreationTime { get; set; }
        public string AppId { get; set; }
        public string ExeName { get; set; }
        public uint OriginalControlMask { get; set; }
        public uint OriginalStateMask { get; set; }
        public uint OriginalPriorityClass { get; set; }
        public bool HasOriginalEcoQuery { get; set; }
    }

    public sealed class MatchedProcessInfo
    {
        public int Pid { get; set; }
        public int ParentPid { get; set; }
        public long CreationTime { get; set; }
        public string ExeName { get; set; }
        public string FullPath { get; set; }
        public string AppId { get; set; }
        public string AppDisplayName { get; set; }
        public string MatchSource { get; set; }
        public bool IsEcoEnabled { get; set; }
        public uint PriorityRaw { get; set; }
        public string PriorityText { get; set; }
        public double WorkingSetMb { get; set; }
        public bool IsManagedByTool { get; set; }
        public string LastActionText { get; set; }
    }

    public sealed class RunningAppCandidate
    {
        public string DisplayName { get; set; }
        public string WindowTitle { get; set; }
        public string PrimaryExe { get; set; }
        public string FullPath { get; set; }
        public string InstallDir { get; set; }
        public List<string> DetectedExes { get; set; }
        public List<string> PreviewProcessDetails { get; set; }
        public int RunningProcessCount { get; set; }
        public double TotalMemoryMb { get; set; }

        public RunningAppCandidate()
        {
            DetectedExes = new List<string>();
            PreviewProcessDetails = new List<string>();
        }
    }

    public sealed class AppRuntimeSummary
    {
        public string AppId { get; set; }
        public int RunningCount { get; set; }
        public int EcoActiveCount { get; set; }
        public bool IsForegroundExempt { get; set; }
        public double TotalWorkingSetMb { get; set; }
        public List<MatchedProcessInfo> Processes { get; set; }

        public AppRuntimeSummary()
        {
            Processes = new List<MatchedProcessInfo>();
        }
    }

    public sealed class RestoreResult
    {
        public int RestoredCount { get; set; }
        public int SkippedUnmanagedCount { get; set; }
        public int SkippedExitedOrReusedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> FailureDetails { get; set; }

        public RestoreResult()
        {
            FailureDetails = new List<string>();
        }
    }

    public sealed class WatcherPassResult
    {
        public int TotalTrackedProcesses { get; set; }
        public int TotalEcoActiveProcesses { get; set; }
        public int NewlyAppliedCount { get; set; }
        public int SkippedAlreadyActiveCount { get; set; }
        public int SkippedForegroundCount { get; set; }
        public int FailedCount { get; set; }
        public int ForegroundRestoredCount { get; set; }
        public double TotalTrimmedMb { get; set; }
        public bool ConfigModified { get; set; }
        public string ForegroundAppId { get; set; }
        public Dictionary<string, AppRuntimeSummary> PerApp { get; set; }
        public List<string> FailureDetails { get; set; }
        public List<string> LogLines { get; set; }

        public WatcherPassResult()
        {
            PerApp = new Dictionary<string, AppRuntimeSummary>();
            FailureDetails = new List<string>();
            LogLines = new List<string>();
        }
    }

    // ========================
    // 2. Guard / Update / SAC Models
    // ========================

    public enum DefenderState
    {
        Running,
        RealtimeOff,
        Disabled,
        StoppedButEnabled,
        NotFound,
        Unknown
    }

    public enum TamperState
    {
        Enabled,
        Disabled,
        UnsupportedOrUnknown
    }

    public sealed class DefenderStatusInfo
    {
        public DefenderState State { get; set; }
        public TamperState Tamper { get; set; }
        public string SummaryText { get; set; }
        public string ShortBadgeText { get; set; }
        public string DetailText { get; set; }
        public StatusTone Tone { get; set; }
    }

    public enum WindowsUpdateState
    {
        EnabledRunning,
        EnabledStopped,
        DisabledProtected,
        DisabledUnprotected,
        NotFound,
        Unknown
    }

    public sealed class WindowsUpdateStatusInfo
    {
        public WindowsUpdateState State { get; set; }
        public bool IsProtected { get; set; }
        public bool IsStoreInstallDisabled { get; set; }
        public string SummaryText { get; set; }
        public string ShortBadgeText { get; set; }
        public string DetailText { get; set; }
        public StatusTone Tone { get; set; }
    }

    public sealed class WindowsUpdateOptions
    {
        public bool ProtectServices { get; set; }
        public bool DisableStoreInstallService { get; set; }
        public bool HideTrayIcon { get; set; }

        public WindowsUpdateOptions()
        {
            ProtectServices = true;
            DisableStoreInstallService = false;
            HideTrayIcon = true;
        }
    }

    public enum SmartAppControlState
    {
        On,
        Evaluation,
        Off,
        Unsupported
    }

    public sealed class SmartAppControlStatusInfo
    {
        public SmartAppControlState State { get; set; }
        public int? RawValue { get; set; }
        public string SummaryText { get; set; }
        public string ShortBadgeText { get; set; }
        public string DetailText { get; set; }
        public StatusTone Tone { get; set; }
    }

    public sealed class OperationResult
    {
        public bool Success { get; set; }
        public bool RequiresTamperDisable { get; set; }
        public bool RebootRecommended { get; set; }
        public string Message { get; set; }
        public List<string> Logs { get; private set; }

        public OperationResult()
        {
            Logs = new List<string>();
        }
    }
}
