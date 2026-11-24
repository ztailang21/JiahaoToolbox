using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class EcoPage : Panel
    {
        private readonly ToolboxConfig _config;
        private readonly EcoEngine _engine;
        private readonly ToolTip _toolTip;
        private bool _loadingUi;
        private bool _listReadyForUserCheck;
        private bool _watcherActive;
        private WatcherPassResult _lastPass;

        private StatusPill _pillWatcher;
        private FluentButton _btnToggleWatcher;
        private ListView _lvApps;
        private CheckBox _chkFgExempt;
        private ComboBox _cmbPriority;
        private TextBox _txtLog;

        public event Action<bool> RequestToggleWatcher;
        public event Action<bool, bool> RequestRunPass;
        public event Action RequestEcoStateChanged;

        public EcoPage(ToolboxConfig config, EcoEngine engine)
        {
            _config = config;
            _engine = engine;
            _toolTip = new ToolTip();
            _toolTip.InitialDelay = 250;
            _toolTip.AutoPopDelay = 10000;

            BackColor = Theme.ContentBg;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            BuildLayout();
            SyncFromConfig();
            RebuildAppListView();
        }

        private void BuildLayout()
        {
            Label lblTitle = new Label();
            lblTitle.Text = "效率管理";
            lblTitle.Font = Theme.FontPageTitle();
            lblTitle.ForeColor = Theme.TextPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(26, 18);
            Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "后台自动应用 EcoQoS 效率模式，切回前台即时恢复原状态";
            lblSub.Font = Theme.FontPageSub();
            lblSub.ForeColor = Theme.TextMuted;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(28, 50);
            Controls.Add(lblSub);

            _pillWatcher = new StatusPill();
            _pillWatcher.Location = new Point(392, 23);
            _pillWatcher.Size = new Size(122, 26);
            _pillWatcher.Tone = StatusTone.Gray;
            _pillWatcher.PillText = "监控已停止";
            Controls.Add(_pillWatcher);

            _btnToggleWatcher = FluentFactory.Button("开启自动监控", 522, 20, 126, 32, FluentButtonStyle.PrimaryBlue);
            _btnToggleWatcher.Font = Theme.FontBodyBold();
            _btnToggleWatcher.Click += delegate
            {
                if (RequestToggleWatcher != null) RequestToggleWatcher(!_watcherActive);
            };
            Controls.Add(_btnToggleWatcher);

            // Toolbar card
            RoundedCardPanel barCard = new RoundedCardPanel();
            barCard.CornerRadius = 10;
            barCard.Location = new Point(26, 78);
            barCard.Size = new Size(622, 46);
            Controls.Add(barCard);

            FluentButton btnAddRunning = FluentFactory.Button("+ 运行中应用", 10, 9, 108, 28, FluentButtonStyle.PrimaryBlue);
            btnAddRunning.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold);
            btnAddRunning.Click += OnAddRunningAppClick;
            barCard.Controls.Add(btnAddRunning);

            FluentButton btnAddFile = FluentFactory.Button("+ 选择程序", 124, 9, 88, 28, FluentButtonStyle.Outline);
            btnAddFile.Font = Theme.FontSmall();
            btnAddFile.Click += OnAddFromFileClick;
            barCard.Controls.Add(btnAddFile);

            FluentButton btnInspect = FluentFactory.Button("查看进程", 218, 9, 80, 28, FluentButtonStyle.Outline);
            btnInspect.Font = Theme.FontSmall();
            btnInspect.Click += OnViewProcessesClick;
            barCard.Controls.Add(btnInspect);

            FluentButton btnRemove = FluentFactory.Button("移除", 304, 9, 58, 28, FluentButtonStyle.SoftDanger);
            btnRemove.Font = Theme.FontSmall();
            btnRemove.Click += OnRemoveAppClick;
            barCard.Controls.Add(btnRemove);

            FluentButton btnApplyOnce = FluentFactory.Button("立即生效", 446, 9, 80, 28, FluentButtonStyle.SoftSuccess);
            btnApplyOnce.Font = Theme.FontSmall();
            btnApplyOnce.Click += delegate
            {
                if (RequestRunPass != null) RequestRunPass(true, true);
            };
            barCard.Controls.Add(btnApplyOnce);

            FluentButton btnRestoreAll = FluentFactory.Button("精准还原", 532, 9, 80, 28, FluentButtonStyle.SoftWarning);
            btnRestoreAll.Font = Theme.FontSmall();
            btnRestoreAll.Click += OnRestoreAllClick;
            barCard.Controls.Add(btnRestoreAll);

            _toolTip.SetToolTip(btnAddRunning, "扫描当前运行中的桌面应用，支持启用前预览安装目录与受影响进程");
            _toolTip.SetToolTip(btnAddFile, "手动浏览选择任意 .exe 程序或 .lnk 快捷方式");
            _toolTip.SetToolTip(btnRestoreAll, "停止自动监控并仅还原由本工具修改过的进程原状态");

            // App list card
            RoundedCardPanel listCard = new RoundedCardPanel();
            listCard.CornerRadius = 12;
            listCard.Location = new Point(26, 134);
            listCard.Size = new Size(622, 314);
            Controls.Add(listCard);

            _lvApps = new ListView();
            _lvApps.Location = new Point(12, 12);
            _lvApps.Size = new Size(598, 290);
            _lvApps.View = View.Details;
            _lvApps.FullRowSelect = true;
            _lvApps.CheckBoxes = true;
            _lvApps.GridLines = true;
            _lvApps.BorderStyle = BorderStyle.FixedSingle;
            _lvApps.BackColor = Color.White;
            _lvApps.Font = Theme.FontSmall();
            _lvApps.Columns.Add("应用名称", 126);
            _lvApps.Columns.Add("主程序", 132);
            _lvApps.Columns.Add("效率 / 运行", 82);
            _lvApps.Columns.Add("内存占用", 72);
            _lvApps.Columns.Add("实时状态", 166);
            _lvApps.ItemChecked += OnAppItemChecked;
            _lvApps.HandleCreated += OnListViewHandleCreated;
            _lvApps.DoubleClick += OnViewProcessesClick;
            listCard.Controls.Add(_lvApps);

            // Bottom quick strategy + log card
            RoundedCardPanel bottomCard = new RoundedCardPanel();
            bottomCard.CornerRadius = 12;
            bottomCard.Location = new Point(26, 458);
            bottomCard.Size = new Size(622, 124);
            Controls.Add(bottomCard);

            _chkFgExempt = new CheckBox();
            _chkFgExempt.Text = "前台自动豁免";
            _chkFgExempt.AutoSize = true;
            _chkFgExempt.BackColor = Color.White;
            _chkFgExempt.ForeColor = Theme.TextPrimary;
            _chkFgExempt.Font = Theme.FontSmall();
            _chkFgExempt.Location = new Point(14, 11);
            _chkFgExempt.CheckedChanged += OnQuickPolicyChanged;
            bottomCard.Controls.Add(_chkFgExempt);

            Label lblPrio = new Label();
            lblPrio.Text = "后台优先级:";
            lblPrio.AutoSize = true;
            lblPrio.BackColor = Color.White;
            lblPrio.ForeColor = Theme.TextSecondary;
            lblPrio.Font = Theme.FontSmall();
            lblPrio.Location = new Point(132, 13);
            bottomCard.Controls.Add(lblPrio);

            _cmbPriority = new ComboBox();
            _cmbPriority.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPriority.Font = Theme.FontSmall();
            _cmbPriority.Location = new Point(206, 9);
            _cmbPriority.Size = new Size(146, 22);
            _cmbPriority.Items.Add("低 (Idle · 绿叶)");
            _cmbPriority.Items.Add("低于正常");
            _cmbPriority.SelectedIndexChanged += OnQuickPolicyChanged;
            bottomCard.Controls.Add(_cmbPriority);

            Label lblDblHint = new Label();
            lblDblHint.Text = "双击应用行查看关联进程详情";
            lblDblHint.BackColor = Color.White;
            lblDblHint.ForeColor = Theme.TextMuted;
            lblDblHint.Font = new Font("Microsoft YaHei UI", 8F);
            lblDblHint.TextAlign = ContentAlignment.MiddleRight;
            lblDblHint.Location = new Point(388, 11);
            lblDblHint.Size = new Size(220, 18);
            bottomCard.Controls.Add(lblDblHint);

            _txtLog = new TextBox();
            _txtLog.Multiline = true;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = ScrollBars.Vertical;
            _txtLog.WordWrap = true;
            _txtLog.BorderStyle = BorderStyle.FixedSingle;
            _txtLog.BackColor = Color.FromArgb(248, 250, 252);
            _txtLog.ForeColor = Theme.TextPrimary;
            _txtLog.Font = Theme.FontMono();
            _txtLog.Location = new Point(14, 38);
            _txtLog.Size = new Size(594, 74);
            bottomCard.Controls.Add(_txtLog);
        }

        public void SyncFromConfig()
        {
            _loadingUi = true;
            try
            {
                _chkFgExempt.Checked = _config.EnableForegroundExemption;
                _cmbPriority.SelectedIndex = string.Equals(_config.PriorityClass, "below_normal", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
            finally
            {
                _loadingUi = false;
            }
        }

        public void SetWatcherVisual(bool active)
        {
            _watcherActive = active;
            if (active)
            {
                _pillWatcher.Tone = StatusTone.Green;
                _pillWatcher.PillText = "监控运行中";
                _btnToggleWatcher.Text = "停止自动监控";
                _btnToggleWatcher.ButtonStyle = FluentButtonStyle.SoftDanger;
            }
            else
            {
                _pillWatcher.Tone = StatusTone.Gray;
                _pillWatcher.PillText = "监控已停止";
                _btnToggleWatcher.Text = "开启自动监控";
                _btnToggleWatcher.ButtonStyle = FluentButtonStyle.PrimaryBlue;
            }
        }

        public void ApplyPassResult(WatcherPassResult pass)
        {
            _lastPass = pass;
            UpdateListViewSummaries();
        }

        public void RebuildAppListView()
        {
            _loadingUi = true;
            try
            {
                _lvApps.BeginUpdate();
                _lvApps.Items.Clear();

                for (int i = 0; i < _config.Apps.Count; i++)
                {
                    TargetAppEntry app = _config.Apps[i];
                    ListViewItem item = new ListViewItem(app.DisplayName);
                    item.Checked = app.Enabled;
                    item.SubItems.Add(app.PrimaryExe);
                    item.SubItems.Add("-");
                    item.SubItems.Add("-");
                    item.SubItems.Add(app.Enabled ? "等待检测" : "未勾选");
                    item.Tag = app;
                    _lvApps.Items.Add(item);
                }
                _lvApps.EndUpdate();
            }
            finally
            {
                _loadingUi = false;
            }
            UpdateListViewSummaries();
        }

        private void UpdateListViewSummaries()
        {
            if (_lvApps == null || _lvApps.IsDisposed) return;
            _lvApps.BeginUpdate();
            for (int i = 0; i < _lvApps.Items.Count; i++)
            {
                ListViewItem item = _lvApps.Items[i];
                TargetAppEntry app = item.Tag as TargetAppEntry;
                if (app == null) continue;

                AppRuntimeSummary sum = null;
                if (_lastPass != null && _lastPass.PerApp != null)
                {
                    _lastPass.PerApp.TryGetValue(app.Id, out sum);
                }

                if (sum == null || sum.RunningCount == 0)
                {
                    item.SubItems[2].Text = "0 / 0";
                    item.SubItems[3].Text = "-";
                    item.SubItems[4].Text = app.Enabled ? "未运行（启动后自动捕获）" : "未勾选";
                    item.ForeColor = Theme.TextMuted;
                }
                else
                {
                    item.SubItems[2].Text = string.Format("{0} / {1}", sum.EcoActiveCount, sum.RunningCount);
                    item.SubItems[3].Text = string.Format("{0:F0} MB", sum.TotalWorkingSetMb);
                    if (!app.Enabled)
                    {
                        item.SubItems[4].Text = "运行中（未启用接管）";
                        item.ForeColor = Theme.TextMuted;
                    }
                    else if (sum.IsForegroundExempt)
                    {
                        item.SubItems[4].Text = "前台使用中（自动豁免）";
                        item.ForeColor = Theme.BrandBlueText;
                    }
                    else if (sum.EcoActiveCount >= sum.RunningCount)
                    {
                        item.SubItems[4].Text = "后台效率模式生效中";
                        item.ForeColor = Theme.GreenText;
                    }
                    else if (sum.EcoActiveCount > 0)
                    {
                        item.SubItems[4].Text = "部分进程已开启效率模式";
                        item.ForeColor = Theme.AmberText;
                    }
                    else
                    {
                        item.SubItems[4].Text = _watcherActive ? "待后台接管" : "待开启监控";
                        item.ForeColor = Theme.TextPrimary;
                    }
                }
            }
            _lvApps.EndUpdate();
        }

        public void AppendLog(string msg)
        {
            if (_txtLog == null || _txtLog.IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), msg);
                return;
            }
            string line = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, msg);
            if (_txtLog.TextLength > 18000)
            {
                _txtLog.Text = _txtLog.Text.Substring(_txtLog.TextLength - 9000);
            }
            if (_txtLog.TextLength > 0) _txtLog.AppendText(Environment.NewLine);
            _txtLog.AppendText(line);
            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.ScrollToCaret();
        }

        private void OnListViewHandleCreated(object sender, EventArgs e)
        {
            _listReadyForUserCheck = false;
            BeginInvoke(new Action(delegate { _listReadyForUserCheck = true; }));
        }

        private void OnAppItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loadingUi || !_listReadyForUserCheck || !Visible || e == null || e.Item == null) return;
            TargetAppEntry app = e.Item.Tag as TargetAppEntry;
            if (app == null) return;
            if (app.Enabled == e.Item.Checked) return;

            app.Enabled = e.Item.Checked;
            _config.Save();

            if (!app.Enabled)
            {
                List<string> logs = new List<string>();
                RestoreResult rr = _engine.RestoreNormalMode(_config, app.Id, logs);
                AppendLog(string.Format("已取消勾选「{0}」：精准还原 {1} 个进程。", app.DisplayName, rr.RestoredCount));
            }
            else
            {
                AppendLog(string.Format("已勾选「{0}」加入效率管理。", app.DisplayName));
            }

            if (RequestEcoStateChanged != null) RequestEcoStateChanged();
            if (RequestRunPass != null) RequestRunPass(_watcherActive, false);
        }

        private void OnQuickPolicyChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            _config.EnableForegroundExemption = _chkFgExempt.Checked;
            _config.PriorityClass = _cmbPriority.SelectedIndex == 1 ? "below_normal" : "idle";
            _config.Save();
            _engine.ResetAppliedCache();
            AppendLog(string.Format("策略已更新：前台豁免={0}，后台优先级={1}",
                _config.EnableForegroundExemption ? "开启" : "关闭", _cmbPriority.Text));
            if (RequestEcoStateChanged != null) RequestEcoStateChanged();
            if (_watcherActive && RequestRunPass != null) RequestRunPass(true, false);
        }

        private void OnAddRunningAppClick(object sender, EventArgs e)
        {
            using (AppPickerDialog dlg = new AppPickerDialog(_engine, _config))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                int added = 0;
                foreach (RunningAppCandidate c in dlg.SelectedCandidates)
                {
                    if (HasAppByExeOrDir(c.PrimaryExe, c.InstallDir)) continue;
                    TargetAppEntry entry = new TargetAppEntry();
                    entry.DisplayName = c.DisplayName;
                    entry.PrimaryExe = c.PrimaryExe;
                    entry.InstallDir = c.InstallDir;
                    entry.Enabled = true;
                    entry.KnownExes = new List<string>(c.DetectedExes);
                    if (!ToolboxConfig.ContainsIgnoreCase(entry.KnownExes, entry.PrimaryExe))
                    {
                        entry.KnownExes.Insert(0, entry.PrimaryExe);
                    }
                    _config.Apps.Add(entry);
                    added++;
                }
                if (added > 0)
                {
                    _config.Save();
                    AppendLog(string.Format("已从运行中应用添加 {0} 个程序。", added));
                    RebuildAppListView();
                    if (RequestEcoStateChanged != null) RequestEcoStateChanged();
                    if (RequestRunPass != null) RequestRunPass(_watcherActive, false);
                }
            }
        }

        private void OnAddFromFileClick(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "选择要管理的程序或快捷方式";
                ofd.Filter = "可执行程序或快捷方式 (*.exe;*.lnk)|*.exe;*.lnk|所有文件 (*.*)|*.*";
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(FindForm()) != DialogResult.OK) return;

                string resolved = ofd.FileName;
                if (resolved.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string target = Win32Native.ResolveShortcutTarget(resolved);
                    if (string.IsNullOrEmpty(target) || !File.Exists(target))
                    {
                        MessageBox.Show(FindForm(), "无法解析该快捷方式指向的程序路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    resolved = target;
                }

                string exeName = Path.GetFileName(resolved);
                string installDir = EcoEngine.DeriveAppRootDir(resolved);
                if (HasAppByExeOrDir(exeName, installDir))
                {
                    MessageBox.Show(FindForm(), "该应用已在监控列表中。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                TargetAppEntry entry = new TargetAppEntry();
                entry.DisplayName = Win32Native.GetFriendlyAppName(resolved, exeName);
                entry.PrimaryExe = exeName;
                entry.InstallDir = installDir;
                entry.Enabled = true;
                entry.KnownExes.Add(exeName);
                _config.Apps.Add(entry);
                _config.Save();

                AppendLog(string.Format("已添加程序：{0}（{1}）。", entry.DisplayName, exeName));
                RebuildAppListView();
                if (RequestEcoStateChanged != null) RequestEcoStateChanged();
                if (RequestRunPass != null) RequestRunPass(_watcherActive, false);
            }
        }

        private void OnViewProcessesClick(object sender, EventArgs e)
        {
            if (_lvApps.SelectedItems.Count == 0)
            {
                MessageBox.Show(FindForm(), "请先在列表中选中一个应用。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            TargetAppEntry app = _lvApps.SelectedItems[0].Tag as TargetAppEntry;
            if (app == null) return;
            using (ProcessDetailDialog dlg = new ProcessDetailDialog(app, _engine, _config))
            {
                dlg.ShowDialog(FindForm());
            }
            if (RequestRunPass != null) RequestRunPass(false, false);
        }

        private void OnRemoveAppClick(object sender, EventArgs e)
        {
            if (_lvApps.SelectedItems.Count == 0)
            {
                MessageBox.Show(FindForm(), "请先选中要移除的应用。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            TargetAppEntry app = _lvApps.SelectedItems[0].Tag as TargetAppEntry;
            if (app == null) return;

            DialogResult dr = MessageBox.Show(
                FindForm(),
                string.Format("确定从监控列表移除「{0}」吗？\r\n（移除时会自动还原由本工具修改过的进程原状态）", app.DisplayName),
                "确认移除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            List<string> logs = new List<string>();
            _engine.RestoreNormalMode(_config, app.Id, logs);
            _config.Apps.Remove(app);
            _config.Save();

            AppendLog(string.Format("已移除应用「{0}」。", app.DisplayName));
            for (int i = 0; i < logs.Count; i++) AppendLog(logs[i]);

            RebuildAppListView();
            if (RequestEcoStateChanged != null) RequestEcoStateChanged();
            if (RequestRunPass != null) RequestRunPass(false, false);
        }

        private void OnRestoreAllClick(object sender, EventArgs e)
        {
            if (_watcherActive && RequestToggleWatcher != null)
            {
                RequestToggleWatcher(false);
            }
            List<string> logs = new List<string>();
            RestoreResult rr = _engine.RestoreNormalMode(_config, null, logs);
            AppendLog(string.Format("精准还原完成：恢复原状态 {0} 个，跳过未修改 {1} 个。", rr.RestoredCount, rr.SkippedUnmanagedCount));
            for (int i = 0; i < logs.Count; i++) AppendLog(logs[i]);
            if (RequestRunPass != null) RequestRunPass(false, false);
        }

        private bool HasAppByExeOrDir(string primaryExe, string installDir)
        {
            for (int i = 0; i < _config.Apps.Count; i++)
            {
                TargetAppEntry a = _config.Apps[i];
                if (!string.IsNullOrEmpty(installDir) && !string.IsNullOrEmpty(a.InstallDir))
                {
                    if (string.Equals(EcoEngine.NormalizeDir(a.InstallDir), EcoEngine.NormalizeDir(installDir), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    continue;
                }
                if (!string.IsNullOrEmpty(primaryExe) &&
                    string.Equals(a.PrimaryExe, primaryExe, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
