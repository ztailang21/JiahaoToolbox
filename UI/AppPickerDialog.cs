using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class AppPickerDialog : Form
    {
        private readonly EcoEngine _engine;
        private readonly List<TargetAppEntry> _existingApps;
        private List<RunningAppCandidate> _allCandidates = new List<RunningAppCandidate>();

        private TextBox _txtSearch;
        private ListView _listView;
        private TextBox _txtPreview;
        private Label _lblHint;
        private FluentButton _btnRefresh;
        private FluentButton _btnOk;
        private FluentButton _btnCancel;

        public List<RunningAppCandidate> SelectedCandidates { get; private set; }

        public AppPickerDialog(EcoEngine engine, ToolboxConfig config)
        {
            _engine = engine;
            _existingApps = (config != null && config.Apps != null)
                ? new List<TargetAppEntry>(config.Apps)
                : new List<TargetAppEntry>();
            SelectedCandidates = new List<RunningAppCandidate>();

            InitializeLayout();
            ReloadCandidates();
        }

        private bool IsAlreadyAdded(RunningAppCandidate c)
        {
            if (c == null) return false;
            for (int i = 0; i < _existingApps.Count; i++)
            {
                TargetAppEntry a = _existingApps[i];
                if (!string.IsNullOrEmpty(c.InstallDir) && !string.IsNullOrEmpty(a.InstallDir))
                {
                    if (string.Equals(EcoEngine.NormalizeDir(c.InstallDir), EcoEngine.NormalizeDir(a.InstallDir), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    continue;
                }
                if (!string.IsNullOrEmpty(c.PrimaryExe) &&
                    string.Equals(a.PrimaryExe, c.PrimaryExe, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void InitializeLayout()
        {
            Text = "从运行中应用选择（预览受影响目录与进程）";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            BackColor = Theme.ContentBg;
            Font = Theme.FontBody();
            ClientSize = new Size(710, 526);

            Label lblTop = new Label();
            lblTop.Text = "勾选需要纳管的应用（严格限定在可信安装目录及实时父子进程树内，绝不误伤其他目录同名进程）：";
            lblTop.ForeColor = Theme.TextSecondary;
            lblTop.Location = new Point(14, 12);
            lblTop.Size = new Size(682, 20);
            Controls.Add(lblTop);

            _txtSearch = new TextBox();
            _txtSearch.Location = new Point(14, 36);
            _txtSearch.Size = new Size(582, 24);
            _txtSearch.TextChanged += delegate { PopulateList(); };
            Controls.Add(_txtSearch);

            _btnRefresh = FluentFactory.Button("刷新列表", 604, 34, 92, 28, FluentButtonStyle.Outline);
            _btnRefresh.Click += delegate { ReloadCandidates(); };
            Controls.Add(_btnRefresh);

            _listView = new ListView();
            _listView.Location = new Point(14, 68);
            _listView.Size = new Size(682, 276);
            _listView.View = View.Details;
            _listView.FullRowSelect = true;
            _listView.CheckBoxes = true;
            _listView.GridLines = true;
            _listView.BackColor = Color.White;
            _listView.Columns.Add("应用名称", 152);
            _listView.Columns.Add("主程序", 124);
            _listView.Columns.Add("受影响进程", 82);
            _listView.Columns.Add("内存", 70);
            _listView.Columns.Add("可信追踪目录", 244);
            _listView.ItemCheck += OnItemCheck;
            _listView.SelectedIndexChanged += OnSelectedCandidateChanged;
            Controls.Add(_listView);

            Label lblPreview = new Label();
            lblPreview.Text = "启用前预览 — 选中应用将受影响的进程及完整路径：";
            lblPreview.ForeColor = Theme.TextPrimary;
            lblPreview.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold);
            lblPreview.Location = new Point(14, 352);
            lblPreview.Size = new Size(682, 18);
            Controls.Add(lblPreview);

            _txtPreview = new TextBox();
            _txtPreview.Location = new Point(14, 372);
            _txtPreview.Size = new Size(682, 94);
            _txtPreview.Multiline = true;
            _txtPreview.ReadOnly = true;
            _txtPreview.ScrollBars = ScrollBars.Vertical;
            _txtPreview.BackColor = Color.White;
            _txtPreview.ForeColor = Theme.TextPrimary;
            _txtPreview.Font = Theme.FontMono();
            _txtPreview.BorderStyle = BorderStyle.FixedSingle;
            _txtPreview.Text = "点击上方任意应用即可预览其安装目录与当前会被纳管的全部进程路径。";
            Controls.Add(_txtPreview);

            _lblHint = new Label();
            _lblHint.Text = "提示：勾选后点击“添加到监控列表”即可生效";
            _lblHint.ForeColor = Theme.TextMuted;
            _lblHint.Location = new Point(14, 480);
            _lblHint.Size = new Size(440, 24);
            _lblHint.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_lblHint);

            _btnOk = FluentFactory.Button("添加到监控列表", 474, 476, 130, 32, FluentButtonStyle.PrimaryBlue);
            _btnOk.Click += OnConfirmClick;
            Controls.Add(_btnOk);

            _btnCancel = FluentFactory.Button("取消", 612, 476, 84, 32, FluentButtonStyle.Outline);
            _btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(_btnCancel);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void ReloadCandidates()
        {
            _allCandidates = _engine.ScanRunningApps();
            PopulateList();
        }

        private void PopulateList()
        {
            string kw = (_txtSearch.Text ?? "").Trim();
            _listView.BeginUpdate();
            _listView.Items.Clear();

            int shown = 0;
            foreach (RunningAppCandidate c in _allCandidates)
            {
                if (!string.IsNullOrEmpty(kw))
                {
                    bool hit =
                        (c.DisplayName != null && c.DisplayName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (c.PrimaryExe != null && c.PrimaryExe.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (c.InstallDir != null && c.InstallDir.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (c.WindowTitle != null && c.WindowTitle.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!hit) continue;
                }

                string primaryExe = c.PrimaryExe ?? "";
                string displayName = c.DisplayName ?? primaryExe;
                bool alreadyAdded = IsAlreadyAdded(c);
                string nameCol = alreadyAdded ? displayName + " (已添加)" : displayName;
                ListViewItem item = new ListViewItem(nameCol);
                item.SubItems.Add(primaryExe);
                item.SubItems.Add(string.Format("{0} 个", c.RunningProcessCount));
                item.SubItems.Add(string.Format("{0:F0} MB", c.TotalMemoryMb));
                item.SubItems.Add(string.IsNullOrEmpty(c.InstallDir) ? (c.FullPath ?? "") : c.InstallDir);
                item.Tag = c;
                if (alreadyAdded)
                {
                    item.ForeColor = Color.FromArgb(156, 163, 175);
                }
                _listView.Items.Add(item);
                shown++;
            }

            _listView.EndUpdate();
            _lblHint.Text = string.Format("共发现 {0} 个正在运行的桌面应用（点击行可预览受影响进程）", shown);
        }

        private void OnSelectedCandidateChanged(object sender, EventArgs e)
        {
            if (_listView.SelectedItems.Count == 0) return;
            RunningAppCandidate c = _listView.SelectedItems[0].Tag as RunningAppCandidate;
            if (c == null) return;

            List<string> lines = new List<string>();
            lines.Add(string.Format("追踪目录：{0}", string.IsNullOrEmpty(c.InstallDir) ? "（单文件精准路径）" : c.InstallDir));
            lines.Add(string.Format("同目录可信进程名：{0}", string.Join(", ", c.DetectedExes.ToArray())));
            lines.Add("当前受影响进程：");
            for (int i = 0; i < c.PreviewProcessDetails.Count; i++)
            {
                lines.Add("  • " + c.PreviewProcessDetails[i]);
            }
            _txtPreview.Text = string.Join("\r\n", lines.ToArray());
        }

        private void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_listView == null || e == null) return;
            if (e.Index >= 0 && e.Index < _listView.Items.Count)
            {
                RunningAppCandidate c = _listView.Items[e.Index].Tag as RunningAppCandidate;
                if (c != null && IsAlreadyAdded(c) && e.NewValue == CheckState.Checked)
                {
                    e.NewValue = CheckState.Unchecked;
                }
            }
        }

        private void OnConfirmClick(object sender, EventArgs e)
        {
            SelectedCandidates.Clear();
            foreach (ListViewItem item in _listView.CheckedItems)
            {
                RunningAppCandidate c = item.Tag as RunningAppCandidate;
                if (c != null)
                {
                    SelectedCandidates.Add(c);
                }
            }

            if (SelectedCandidates.Count == 0)
            {
                MessageBox.Show(this, "请先勾选至少一个应用。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
