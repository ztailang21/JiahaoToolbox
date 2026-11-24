using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class ProcessDetailDialog : Form
    {
        private readonly TargetAppEntry _app;
        private readonly EcoEngine _engine;
        private readonly ToolboxConfig _config;
        private ListView _listView;
        private Label _lblMeta;
        private Label _lblKnown;
        private Label _lblOpResult;

        public ProcessDetailDialog(TargetAppEntry app, EcoEngine engine, ToolboxConfig config)
        {
            _app = app;
            _engine = engine;
            _config = config;
            InitializeLayout();
            RefreshProcesses(null);
        }

        private void InitializeLayout()
        {
            Text = string.Format("关联进程详情 — {0}", _app.DisplayName);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            BackColor = Theme.ContentBg;
            Font = Theme.FontBody();
            ClientSize = new Size(768, 472);

            _lblMeta = new Label();
            _lblMeta.Location = new Point(14, 12);
            _lblMeta.Size = new Size(740, 20);
            _lblMeta.ForeColor = Theme.TextPrimary;
            _lblMeta.Font = Theme.FontBodyBold();
            Controls.Add(_lblMeta);

            _lblKnown = new Label();
            _lblKnown.Location = new Point(14, 34);
            _lblKnown.Size = new Size(740, 28);
            _lblKnown.ForeColor = Theme.TextMuted;
            _lblKnown.Font = Theme.FontSmall();
            Controls.Add(_lblKnown);

            _listView = new ListView();
            _listView.Location = new Point(14, 66);
            _listView.Size = new Size(740, 312);
            _listView.View = View.Details;
            _listView.FullRowSelect = true;
            _listView.GridLines = true;
            _listView.BackColor = Color.White;
            _listView.Columns.Add("PID", 58);
            _listView.Columns.Add("进程名", 142);
            _listView.Columns.Add("识别方式", 96);
            _listView.Columns.Add("效率模式", 68);
            _listView.Columns.Add("优先级", 74);
            _listView.Columns.Add("内存", 64);
            _listView.Columns.Add("执行结果 / 说明", 132);
            _listView.Columns.Add("完整路径", 220);
            Controls.Add(_listView);

            _lblOpResult = new Label();
            _lblOpResult.Location = new Point(14, 386);
            _lblOpResult.Size = new Size(740, 26);
            _lblOpResult.BackColor = Color.White;
            _lblOpResult.BorderStyle = BorderStyle.FixedSingle;
            _lblOpResult.ForeColor = Theme.TextSecondary;
            _lblOpResult.Font = Theme.FontSmall();
            _lblOpResult.TextAlign = ContentAlignment.MiddleLeft;
            _lblOpResult.Padding = new Padding(8, 0, 8, 0);
            _lblOpResult.Text = "提示：下方操作按钮仅作用于当前应用，不影响列表中其他应用。";
            Controls.Add(_lblOpResult);

            FluentButton btnRefresh = FluentFactory.Button("刷新状态", 14, 424, 92, 32, FluentButtonStyle.Outline);
            btnRefresh.Click += delegate { RefreshProcesses(null); };
            Controls.Add(btnRefresh);

            FluentButton btnApplyNow = FluentFactory.Button("仅对此应用开启效率模式", 114, 424, 174, 32, FluentButtonStyle.PrimaryBlue);
            btnApplyNow.Click += delegate
            {
                WatcherPassResult res = _engine.RunPass(_config, true, false, _app.Id, true);
                if (res.ConfigModified) _config.Save();
                int skippedTotal = res.SkippedAlreadyActiveCount + res.SkippedForegroundCount;
                string skipNote = res.SkippedForegroundCount > 0
                    ? string.Format("跳过 {0}（含前台豁免 {1}）", skippedTotal, res.SkippedForegroundCount)
                    : string.Format("跳过 {0}（已是目标态）", skippedTotal);
                string failNote = res.FailedCount > 0 && res.FailureDetails.Count > 0
                    ? string.Format("失败 {0}（{1}）", res.FailedCount, res.FailureDetails[0])
                    : string.Format("失败 {0}", res.FailedCount);
                _lblOpResult.Text = string.Format("仅「{0}」执行结果：成功 {1} · {2} · {3}",
                    _app.DisplayName, res.NewlyAppliedCount, skipNote, failNote);
                _lblOpResult.ForeColor = res.FailedCount > 0 ? Theme.RedText : Theme.GreenText;
                RefreshProcesses(res);
            };
            Controls.Add(btnApplyNow);

            FluentButton btnRestore = FluentFactory.Button("还原此应用原状态", 296, 424, 140, 32, FluentButtonStyle.SoftWarning);
            btnRestore.Click += delegate
            {
                RestoreResult rr = _engine.RestoreNormalMode(_config, _app.Id, null);
                string failNote = rr.FailedCount > 0 && rr.FailureDetails.Count > 0
                    ? string.Format("失败 {0}（{1}）", rr.FailedCount, rr.FailureDetails[0])
                    : string.Format("失败 {0}", rr.FailedCount);
                _lblOpResult.Text = string.Format("仅「{0}」精准还原：还原原状态 {1} · 跳过 {2}（非本工具修改）· {3}",
                    _app.DisplayName, rr.RestoredCount, rr.SkippedUnmanagedCount, failNote);
                _lblOpResult.ForeColor = rr.FailedCount > 0 ? Theme.RedText : Theme.AmberText;
                RefreshProcesses(null);
            };
            Controls.Add(btnRestore);

            FluentButton btnClose = FluentFactory.Button("关闭", 670, 424, 84, 32, FluentButtonStyle.Outline);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);
            AcceptButton = btnClose;
        }

        private void RefreshProcesses(WatcherPassResult existingPass)
        {
            string dirText = string.IsNullOrEmpty(_app.InstallDir) ? "（运行后自动学习安装目录，仅限同目录内学习）" : _app.InstallDir;
            _lblMeta.Text = string.Format("主程序：{0}   |   可信追踪目录：{1}", _app.PrimaryExe, dirText);
            _lblKnown.Text = string.Format("同目录已识别进程名（{0} 个）：{1}",
                _app.KnownExes.Count,
                string.Join(", ", _app.KnownExes.ToArray()));

            WatcherPassResult pass = existingPass ?? _engine.RunPass(_config, false, false, _app.Id, false);
            if (existingPass == null && pass.ConfigModified) _config.Save();

            AppRuntimeSummary sum;
            List<MatchedProcessInfo> list = pass.PerApp.TryGetValue(_app.Id, out sum)
                ? sum.Processes
                : new List<MatchedProcessInfo>();

            _listView.BeginUpdate();
            _listView.Items.Clear();
            foreach (MatchedProcessInfo mp in list)
            {
                ListViewItem item = new ListViewItem(mp.Pid.ToString());
                item.SubItems.Add(mp.ExeName);
                item.SubItems.Add(mp.MatchSource);
                item.SubItems.Add(mp.IsEcoEnabled ? "已开启" : "未开启");
                item.SubItems.Add(mp.PriorityText);
                item.SubItems.Add(string.Format("{0:F1} MB", mp.WorkingSetMb));
                string actionText = !string.IsNullOrEmpty(mp.LastActionText)
                    ? mp.LastActionText
                    : (mp.IsManagedByTool ? "本工具接管中" : "未修改");
                item.SubItems.Add(actionText);
                item.SubItems.Add(string.IsNullOrEmpty(mp.FullPath) ? "（路径受限）" : mp.FullPath);

                if (actionText.StartsWith("失败"))
                {
                    item.ForeColor = Theme.RedText;
                }
                else if (actionText.IndexOf("前台") >= 0)
                {
                    item.ForeColor = Theme.BrandBlueText;
                }
                else if (mp.IsEcoEnabled)
                {
                    item.ForeColor = Theme.GreenText;
                }
                _listView.Items.Add(item);
            }
            _listView.EndUpdate();

            if (list.Count == 0)
            {
                ListViewItem empty = new ListViewItem("-");
                empty.SubItems.Add("当前未运行（打开该应用后将自动捕获并接管）");
                empty.ForeColor = Theme.GrayText;
                _listView.Items.Add(empty);
            }
        }
    }
}
