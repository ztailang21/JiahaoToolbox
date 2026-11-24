using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class MainForm : Form
    {
        private readonly ToolboxConfig _config;
        private readonly EcoEngine _ecoEngine;
        private readonly bool _startInTray;
        private readonly uint _restoreMsg;
        private readonly string _initialPage;
        private readonly bool _resumeEcoOnStart;
        private readonly bool _readOnlyPreviewMode;
        private readonly Action _releaseSingleInstanceMutex;

        private Panel _headerPanel;
        private Panel _sidebarPanel;
        private Panel _contentHost;

        private SidebarNavItem _navOverview;
        private SidebarNavItem _navEco;
        private SidebarNavItem _navGuard;
        private SidebarNavItem _navSettings;
        private SidebarNavItem[] _navItems;

        private OverviewPage _overviewPage;
        private EcoPage _ecoPage;
        private GuardUpdatePage _guardPage;
        private SettingsPage _settingsPage;

        private NotifyIcon _trayIcon;
        private ToolStripMenuItem _trayToggleWatcherItem;
        private Timer _ecoPollTimer;

        private bool _watcherActive;
        private bool _allowExit;
        private bool _ecoPassBusy;
        private string _currentPageKey = "overview";

        public MainForm(
            bool startInTray,
            uint restoreMsg,
            string initialPage,
            bool resumeEcoOnStart,
            Action releaseSingleInstanceMutex,
            bool readOnlyPreviewMode)
        {
            _startInTray = startInTray;
            _restoreMsg = restoreMsg;
            _initialPage = string.IsNullOrEmpty(initialPage) ? "overview" : initialPage;
            _resumeEcoOnStart = resumeEcoOnStart;
            _releaseSingleInstanceMutex = releaseSingleInstanceMutex;
            _readOnlyPreviewMode = readOnlyPreviewMode;

            _config = ToolboxConfig.Load(readOnlyPreviewMode);
            _ecoEngine = new EcoEngine();

            InitializeWindow();
            BuildShell();
            BuildPages();
            SetupTrayAndTimer();

            NavigateTo(_initialPage);

            if (_startInTray && !_readOnlyPreviewMode)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Opacity = 0;
            }

            Shown += OnMainFormShown;
        }

        private void InitializeWindow()
        {
            Text = "嘉豪工具箱";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            BackColor = Theme.ContentBg;
            Font = Theme.FontBody();
            ClientSize = new Size(860, 650);

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
        }

        private void BuildShell()
        {
            // 1. Top Header Bar
            _headerPanel = new Panel();
            _headerPanel.Location = new Point(0, 0);
            _headerPanel.Size = new Size(860, 52);
            _headerPanel.BackColor = Theme.HeaderBg;
            _headerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Divider, 1f))
                {
                    e.Graphics.DrawLine(p, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
                }
            };
            Controls.Add(_headerPanel);

            IconBadgeBox logoBadge = new IconBadgeBox();
            logoBadge.IconKind = NavIconKind.AppLogo;
            logoBadge.CornerRadius = 8;
            logoBadge.Location = new Point(18, 12);
            logoBadge.Size = new Size(28, 28);
            _headerPanel.Controls.Add(logoBadge);

            Label lblAppName = new Label();
            lblAppName.Text = "嘉豪工具箱";
            lblAppName.Font = Theme.FontAppTitle();
            lblAppName.ForeColor = Theme.TextPrimary;
            lblAppName.BackColor = Theme.HeaderBg;
            lblAppName.AutoSize = true;
            lblAppName.Location = new Point(54, 15);
            _headerPanel.Controls.Add(lblAppName);

            bool isAdmin = ElevationHelper.IsAdministrator();
            StatusPill permPill = new StatusPill();
            permPill.Location = new Point(674, 14);
            permPill.Size = new Size(120, 24);
            permPill.Tone = isAdmin ? StatusTone.Green : StatusTone.Gray;
            permPill.PillText = isAdmin ? "管理员模式" : "普通模式";
            if (!isAdmin)
            {
                permPill.Cursor = Cursors.Hand;
                permPill.Click += delegate { PerformElevatedRestart(_currentPageKey); };
            }
            _headerPanel.Controls.Add(permPill);

            Label lblVer = new Label();
            lblVer.Text = "v1.0.1";
            lblVer.Font = Theme.FontSmall();
            lblVer.ForeColor = Theme.TextMuted;
            lblVer.BackColor = Theme.HeaderBg;
            lblVer.TextAlign = ContentAlignment.MiddleRight;
            lblVer.Location = new Point(798, 16);
            lblVer.Size = new Size(44, 20);
            _headerPanel.Controls.Add(lblVer);

            // 2. Left Sidebar
            _sidebarPanel = new Panel();
            _sidebarPanel.Location = new Point(0, 52);
            _sidebarPanel.Size = new Size(186, 598);
            _sidebarPanel.BackColor = Theme.SidebarBg;
            _sidebarPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Divider, 1f))
                {
                    e.Graphics.DrawLine(p, _sidebarPanel.Width - 1, 0, _sidebarPanel.Width - 1, _sidebarPanel.Height);
                }
            };
            Controls.Add(_sidebarPanel);

            _navOverview = CreateNavItem("总览", "overview", NavIconKind.Overview, 16);
            _navEco = CreateNavItem("效率管理", "eco", NavIconKind.Leaf, 60);
            _navGuard = CreateNavItem("防护与更新", "guard", NavIconKind.Shield, 104);
            _navSettings = CreateNavItem("通用设置", "settings", NavIconKind.Settings, 544);

            _navItems = new SidebarNavItem[] { _navOverview, _navEco, _navGuard, _navSettings };

            // 3. Right Content Host
            _contentHost = new Panel();
            _contentHost.Location = new Point(186, 52);
            _contentHost.Size = new Size(674, 598);
            _contentHost.BackColor = Theme.ContentBg;
            Controls.Add(_contentHost);
        }

        private SidebarNavItem CreateNavItem(string title, string key, NavIconKind icon, int top)
        {
            SidebarNavItem item = new SidebarNavItem();
            item.Title = title;
            item.PageKey = key;
            item.IconKind = icon;
            item.Location = new Point(12, top);
            item.Size = new Size(162, 38);
            item.Click += delegate { NavigateTo(key); };
            _sidebarPanel.Controls.Add(item);
            return item;
        }

        private void BuildPages()
        {
            _overviewPage = new OverviewPage();
            _overviewPage.NavigateToEco += delegate { NavigateTo("eco"); };
            _overviewPage.NavigateToGuard += delegate { NavigateTo("guard"); };
            _overviewPage.RequestRefreshAll += async delegate
            {
                RunEcoPass(false, false);
                await _guardPage.RefreshStatusesAsync(false);
            };

            _ecoPage = new EcoPage(_config, _ecoEngine);
            _ecoPage.RequestToggleWatcher += OnToggleWatcherRequested;
            _ecoPage.RequestRunPass += delegate(bool apply, bool force) { RunEcoPass(apply, force); };
            _ecoPage.RequestEcoStateChanged += delegate
            {
                if (_settingsPage != null) _settingsPage.SyncFromConfig();
                if (_overviewPage != null) RefreshOverviewEco();
            };

            _guardPage = new GuardUpdatePage(_config);
            _guardPage.RequestElevateToGuard += delegate { PerformElevatedRestart("guard"); };
            _guardPage.StatusesUpdated += delegate(DefenderStatusInfo d, WindowsUpdateStatusInfo w, SmartAppControlStatusInfo s)
            {
                _overviewPage.UpdateGuardSummary(d, w, s);
            };

            _settingsPage = new SettingsPage(_config);
            _settingsPage.SettingsChanged += delegate
            {
                _ecoPage.SyncFromConfig();
                _ecoEngine.ResetAppliedCache();
                UpdateTimerInterval();
                RefreshOverviewEco();
                if (_watcherActive) RunEcoPass(true, false);
            };
            _settingsPage.RequestImportLegacyConfig += OnImportLegacyConfig;
            _settingsPage.RequestElevate += delegate { PerformElevatedRestart("settings"); };

            _contentHost.Controls.Add(_overviewPage);
            _contentHost.Controls.Add(_ecoPage);
            _contentHost.Controls.Add(_guardPage);
            _contentHost.Controls.Add(_settingsPage);
        }

        private void SetupTrayAndTimer()
        {
            _ecoPollTimer = new Timer();
            UpdateTimerInterval();
            _ecoPollTimer.Tick += delegate
            {
                if (_watcherActive)
                {
                    RunEcoPass(true, false);
                }
            };

            if (_readOnlyPreviewMode) return;

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("显示嘉豪工具箱");
            openItem.Click += delegate { RestoreFromTray(); };

            _trayToggleWatcherItem = new ToolStripMenuItem("开启后台效率监控");
            _trayToggleWatcherItem.Click += delegate { OnToggleWatcherRequested(!_watcherActive); };

            ToolStripMenuItem guardItem = new ToolStripMenuItem("打开防护与更新");
            guardItem.Click += delegate { NavigateTo("guard"); RestoreFromTray(); };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出嘉豪工具箱");
            exitItem.Click += delegate { ExitApplication(); };

            trayMenu.Items.Add(openItem);
            trayMenu.Items.Add(_trayToggleWatcherItem);
            trayMenu.Items.Add(guardItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitItem);

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = Icon ?? SystemIcons.Application;
            _trayIcon.Text = "嘉豪工具箱";
            _trayIcon.ContextMenuStrip = trayMenu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += delegate { RestoreFromTray(); };
        }

        private void UpdateTimerInterval()
        {
            int sec = _config.PollIntervalSeconds;
            if (sec < 2) sec = 4;
            _ecoPollTimer.Interval = sec * 1000;
        }

        public void NavigateTo(string pageKey)
        {
            _currentPageKey = pageKey;
            _overviewPage.Visible = string.Equals(pageKey, "overview", StringComparison.OrdinalIgnoreCase);
            _ecoPage.Visible = string.Equals(pageKey, "eco", StringComparison.OrdinalIgnoreCase);
            _guardPage.Visible = string.Equals(pageKey, "guard", StringComparison.OrdinalIgnoreCase);
            _settingsPage.Visible = string.Equals(pageKey, "settings", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < _navItems.Length; i++)
            {
                _navItems[i].Active = string.Equals(_navItems[i].PageKey, pageKey, StringComparison.OrdinalIgnoreCase);
            }
        }

        private async void OnMainFormShown(object sender, EventArgs e)
        {
            if (_startInTray && !_readOnlyPreviewMode)
            {
                Hide();
                Opacity = 1;
            }

            if (_readOnlyPreviewMode) return;

            if (_config.MigratedFromLegacyEco)
            {
                _ecoPage.AppendLog("已自动继承原 AutoEcoControl 的应用列表与策略配置。");
            }

            bool shouldStartWatcher = _resumeEcoOnStart || _config.AutoStartWatcher || _startInTray;
            if (shouldStartWatcher && _config.EnabledAppCount() > 0)
            {
                StartWatcher();
            }
            else
            {
                RunEcoPass(false, false);
            }

            await _guardPage.RefreshStatusesAsync(false);
        }

        public void LoadReadOnlyPreviewSnapshot()
        {
            WatcherPassResult pass = _ecoEngine.RunPass(_config, false, false, null, false);
            _ecoPage.ApplyPassResult(pass);
            _ecoPage.SetWatcherVisual(_config.AutoStartWatcher && _config.EnabledAppCount() > 0);
            _ecoPage.AppendLog("已加载当前运行状态快照。");

            DefenderStatusInfo def = DefenderManager.GetStatus();
            WindowsUpdateStatusInfo wu = WindowsUpdateManager.GetStatus();
            SmartAppControlStatusInfo sac = SmartAppControlManager.GetStatus();
            _guardPage.ApplyStatuses(def, wu, sac);
            _guardPage.AppendLog("提示", "已检测当前系统防护、更新与智能应用控制状态。");

            _overviewPage.UpdateEcoSummary(
                _config.AutoStartWatcher && _config.EnabledAppCount() > 0,
                _config.EnabledAppCount(),
                _config.EnableForegroundExemption,
                pass.TotalEcoActiveProcesses,
                pass.TotalTrackedProcesses);
            _overviewPage.UpdateGuardSummary(def, wu, sac);
        }

        private void OnToggleWatcherRequested(bool start)
        {
            if (start)
            {
                if (_config.EnabledAppCount() == 0)
                {
                    MessageBox.Show(this, "请先在「效率管理」列表中勾选至少一个需要管理的应用。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                StartWatcher();
            }
            else
            {
                StopWatcher();
            }
        }

        private void StartWatcher()
        {
            _watcherActive = true;
            _ecoPage.SetWatcherVisual(true);
            _ecoPollTimer.Start();
            UpdateTrayLabel();
            _ecoPage.AppendLog("已开启后台自动效率监控。");
            RunEcoPass(true, false);
        }

        private void StopWatcher()
        {
            _watcherActive = false;
            _ecoPollTimer.Stop();
            _ecoPage.SetWatcherVisual(false);
            UpdateTrayLabel();
            _ecoPage.AppendLog("已停止后台自动效率监控。");
            RefreshOverviewEco();
        }

        private void UpdateTrayLabel()
        {
            if (_trayToggleWatcherItem != null)
            {
                _trayToggleWatcherItem.Text = _watcherActive ? "停止后台效率监控" : "开启后台效率监控";
            }
            if (_trayIcon != null)
            {
                _trayIcon.Text = _watcherActive ? "嘉豪工具箱（效率监控运行中）" : "嘉豪工具箱";
            }
        }

        private int _lastEcoActiveProcs;
        private int _lastTrackedProcs;

        private void RunEcoPass(bool applyChanges, bool forceReapply)
        {
            if (_ecoPassBusy) return;
            _ecoPassBusy = true;
            try
            {
                WatcherPassResult res = _ecoEngine.RunPass(_config, applyChanges, forceReapply, null, false);
                if (res.ConfigModified && !_readOnlyPreviewMode)
                {
                    _config.Save();
                }

                _lastEcoActiveProcs = res.TotalEcoActiveProcesses;
                _lastTrackedProcs = res.TotalTrackedProcesses;
                _ecoPage.ApplyPassResult(res);

                if (applyChanges && (res.NewlyAppliedCount > 0 || res.ForegroundRestoredCount > 0 || res.FailedCount > 0))
                {
                    _ecoPage.AppendLog(string.Format("扫描执行：新生效 {0} · 前台恢复 {1} · 失败 {2}",
                        res.NewlyAppliedCount, res.ForegroundRestoredCount, res.FailedCount));
                }
                RefreshOverviewEco();
            }
            catch (Exception ex)
            {
                _ecoPage.AppendLog("效率扫描异常: " + ex.Message);
            }
            finally
            {
                _ecoPassBusy = false;
            }
        }

        private void RefreshOverviewEco()
        {
            _overviewPage.UpdateEcoSummary(
                _watcherActive,
                _config.EnabledAppCount(),
                _config.EnableForegroundExemption,
                _lastEcoActiveProcs,
                _lastTrackedProcs);
        }

        private void OnImportLegacyConfig()
        {
            int count = ToolboxConfig.TryImportLegacyConfigs(_config);
            if (count > 0)
            {
                _config.Save();
                _ecoPage.SyncFromConfig();
                _ecoPage.RebuildAppListView();
                _guardPage.LoadConfigToUi();
                _settingsPage.SyncFromConfig();
                RunEcoPass(false, false);
                MessageBox.Show(this, string.Format("已成功导入原 AutoEcoControl 的 {0} 个应用配置！", count), "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "未在旧版配置文件中找到可导入的应用条目。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void PerformElevatedRestart(string targetPage)
        {
            bool cancelled;
            string err;
            bool launched = ElevationHelper.TryLaunchElevatedRestart(targetPage, _watcherActive, out cancelled, out err);
            if (launched)
            {
                if (_releaseSingleInstanceMutex != null)
                {
                    try { _releaseSingleInstanceMutex(); } catch { }
                }
                ExitApplication();
            }
            else if (!cancelled && !string.IsNullOrEmpty(err))
            {
                MessageBox.Show(this, err, "提权失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            Opacity = 1;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Win32Native.ShowWindow(Handle, Win32Native.SW_RESTORE);
            Activate();
            Win32Native.SetForegroundWindow(Handle);
        }

        private void ExitApplication()
        {
            _allowExit = true;
            if (_ecoPollTimer != null) _ecoPollTimer.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            if (_releaseSingleInstanceMutex != null)
            {
                try { _releaseSingleInstanceMutex(); } catch { }
            }
            try { Close(); } catch { }
            Application.Exit();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_allowExit && !_readOnlyPreviewMode)
            {
                Environment.Exit(0);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowExit && !_readOnlyPreviewMode && _config.MinimizeToTrayOnClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                try
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.ShowBalloonTip(1600, "嘉豪工具箱", "已最小化到右下角系统托盘，双击图标可随时恢复窗口", ToolTipIcon.Info);
                    }
                }
                catch { }
                return;
            }

            if (_ecoPollTimer != null) _ecoPollTimer.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (_restoreMsg != 0 && (uint)m.Msg == _restoreMsg)
            {
                RestoreFromTray();
                return;
            }
            base.WndProc(ref m);
        }
    }
}
