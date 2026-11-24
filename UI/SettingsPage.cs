using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using JiahaoToolbox.Core;

namespace JiahaoToolbox.UI
{
    public sealed class SettingsPage : Panel
    {
        private readonly ToolboxConfig _config;
        private bool _loadingUi;

        private CheckBox _chkAutostart;
        private CheckBox _chkMinToTray;
        private CheckBox _chkAutoStartWatcher;
        private ComboBox _cmbPriority;
        private NumericUpDown _numPollSeconds;
        private CheckBox _chkFgExempt;
        private CheckBox _chkIdleTrim;
        private Label _lblStatusTip;

        public event Action SettingsChanged;
        public event Action RequestImportLegacyConfig;
        public event Action RequestElevate;

        public SettingsPage(ToolboxConfig config)
        {
            _config = config;
            BackColor = Theme.ContentBg;
            Dock = DockStyle.Fill;
            DoubleBuffered = true;

            BuildLayout();
            SyncFromConfig();
        }

        private void BuildLayout()
        {
            Label lblTitle = new Label();
            lblTitle.Text = "通用设置";
            lblTitle.Font = Theme.FontPageTitle();
            lblTitle.ForeColor = Theme.TextPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(26, 18);
            Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "配置开机自启、系统托盘、效率管理策略与配置文件";
            lblSub.Font = Theme.FontPageSub();
            lblSub.ForeColor = Theme.TextMuted;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(28, 50);
            Controls.Add(lblSub);

            // Card 1: Startup & Tray
            RoundedCardPanel cardStart = new RoundedCardPanel();
            cardStart.Location = new Point(26, 84);
            cardStart.Size = new Size(602, 122);
            Controls.Add(cardStart);

            Label lblC1 = new Label();
            lblC1.Text = "启动与托盘驻留";
            lblC1.Font = Theme.FontBodyBold();
            lblC1.ForeColor = Theme.TextPrimary;
            lblC1.BackColor = Color.White;
            lblC1.AutoSize = true;
            lblC1.Location = new Point(18, 12);
            cardStart.Controls.Add(lblC1);

            _chkAutostart = new CheckBox();
            _chkAutostart.Text = "开机静默自启（托盘常驻、免 UAC 弹窗，开启时自动清理旧版自启项）";
            _chkAutostart.AutoSize = true;
            _chkAutostart.BackColor = Color.White;
            _chkAutostart.ForeColor = Theme.TextPrimary;
            _chkAutostart.Font = Theme.FontSmall();
            _chkAutostart.Location = new Point(18, 40);
            _chkAutostart.CheckedChanged += OnAutostartChanged;
            cardStart.Controls.Add(_chkAutostart);

            _chkMinToTray = new CheckBox();
            _chkMinToTray.Text = "点击关闭按钮时最小化到右下角系统托盘（右键托盘图标可彻底退出）";
            _chkMinToTray.AutoSize = true;
            _chkMinToTray.BackColor = Color.White;
            _chkMinToTray.ForeColor = Theme.TextPrimary;
            _chkMinToTray.Font = Theme.FontSmall();
            _chkMinToTray.Location = new Point(18, 66);
            _chkMinToTray.CheckedChanged += OnSettingModified;
            cardStart.Controls.Add(_chkMinToTray);

            _chkAutoStartWatcher = new CheckBox();
            _chkAutoStartWatcher.Text = "启动嘉豪工具箱时自动开启后台效率监控";
            _chkAutoStartWatcher.AutoSize = true;
            _chkAutoStartWatcher.BackColor = Color.White;
            _chkAutoStartWatcher.ForeColor = Theme.TextPrimary;
            _chkAutoStartWatcher.Font = Theme.FontSmall();
            _chkAutoStartWatcher.Location = new Point(18, 92);
            _chkAutoStartWatcher.CheckedChanged += OnSettingModified;
            cardStart.Controls.Add(_chkAutoStartWatcher);

            // Card 2: Eco Engine Strategy
            RoundedCardPanel cardEco = new RoundedCardPanel();
            cardEco.Location = new Point(26, 216);
            cardEco.Size = new Size(602, 134);
            Controls.Add(cardEco);

            Label lblC2 = new Label();
            lblC2.Text = "效率管理策略";
            lblC2.Font = Theme.FontBodyBold();
            lblC2.ForeColor = Theme.TextPrimary;
            lblC2.BackColor = Color.White;
            lblC2.AutoSize = true;
            lblC2.Location = new Point(18, 12);
            cardEco.Controls.Add(lblC2);

            Label lblPrio = new Label();
            lblPrio.Text = "后台进程优先级：";
            lblPrio.AutoSize = true;
            lblPrio.BackColor = Color.White;
            lblPrio.ForeColor = Theme.TextSecondary;
            lblPrio.Font = Theme.FontSmall();
            lblPrio.Location = new Point(18, 42);
            cardEco.Controls.Add(lblPrio);

            _cmbPriority = new ComboBox();
            _cmbPriority.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbPriority.Font = Theme.FontSmall();
            _cmbPriority.Location = new Point(122, 38);
            _cmbPriority.Size = new Size(200, 24);
            _cmbPriority.Items.Add("低 (Idle · 任务管理器绿叶)");
            _cmbPriority.Items.Add("低于正常 (Below Normal)");
            _cmbPriority.SelectedIndexChanged += OnSettingModified;
            cardEco.Controls.Add(_cmbPriority);

            Label lblPoll = new Label();
            lblPoll.Text = "扫描间隔（秒）：";
            lblPoll.AutoSize = true;
            lblPoll.BackColor = Color.White;
            lblPoll.ForeColor = Theme.TextSecondary;
            lblPoll.Font = Theme.FontSmall();
            lblPoll.Location = new Point(352, 42);
            cardEco.Controls.Add(lblPoll);

            _numPollSeconds = new NumericUpDown();
            _numPollSeconds.Minimum = 2;
            _numPollSeconds.Maximum = 30;
            _numPollSeconds.Font = Theme.FontSmall();
            _numPollSeconds.Location = new Point(468, 38);
            _numPollSeconds.Size = new Size(64, 22);
            _numPollSeconds.ValueChanged += OnSettingModified;
            cardEco.Controls.Add(_numPollSeconds);

            _chkFgExempt = new CheckBox();
            _chkFgExempt.Text = "前台自动豁免（推荐：目标应用切到前台立即恢复原优先级与 EcoQoS）";
            _chkFgExempt.AutoSize = true;
            _chkFgExempt.BackColor = Color.White;
            _chkFgExempt.ForeColor = Theme.TextPrimary;
            _chkFgExempt.Font = Theme.FontSmall();
            _chkFgExempt.Location = new Point(18, 72);
            _chkFgExempt.CheckedChanged += OnSettingModified;
            cardEco.Controls.Add(_chkFgExempt);

            _chkIdleTrim = new CheckBox();
            _chkIdleTrim.Text = "后台空闲冷内存清理（工作集缩减，默认关闭，仅建议低内存设备按需开启）";
            _chkIdleTrim.AutoSize = true;
            _chkIdleTrim.BackColor = Color.White;
            _chkIdleTrim.ForeColor = Theme.TextSecondary;
            _chkIdleTrim.Font = Theme.FontSmall();
            _chkIdleTrim.Location = new Point(18, 98);
            _chkIdleTrim.CheckedChanged += OnSettingModified;
            cardEco.Controls.Add(_chkIdleTrim);

            // Card 3: Config, Migration, Permission & About
            RoundedCardPanel cardMeta = new RoundedCardPanel();
            cardMeta.Location = new Point(26, 360);
            cardMeta.Size = new Size(602, 152);
            Controls.Add(cardMeta);

            Label lblC3 = new Label();
            lblC3.Text = "配置、权限与关于";
            lblC3.Font = Theme.FontBodyBold();
            lblC3.ForeColor = Theme.TextPrimary;
            lblC3.BackColor = Color.White;
            lblC3.AutoSize = true;
            lblC3.Location = new Point(18, 12);
            cardMeta.Controls.Add(lblC3);

            Label lblCfgPath = new Label();
            lblCfgPath.Text = "配置位置：" + ToolboxConfig.ConfigFilePath;
            lblCfgPath.Font = new Font("Microsoft YaHei UI", 8F);
            lblCfgPath.ForeColor = Theme.TextMuted;
            lblCfgPath.BackColor = Color.White;
            lblCfgPath.Location = new Point(18, 38);
            lblCfgPath.Size = new Size(566, 18);
            cardMeta.Controls.Add(lblCfgPath);

            FluentButton btnOpenDir = FluentFactory.Button("打开配置目录", 18, 64, 114, 30, FluentButtonStyle.Outline);
            btnOpenDir.Font = Theme.FontSmall();
            btnOpenDir.Click += delegate
            {
                try
                {
                    if (!Directory.Exists(ToolboxConfig.ConfigDir)) Directory.CreateDirectory(ToolboxConfig.ConfigDir);
                    Process.Start("explorer.exe", ToolboxConfig.ConfigDir);
                }
                catch { }
            };
            cardMeta.Controls.Add(btnOpenDir);

            FluentButton btnImportLegacy = FluentFactory.Button("导入旧版效率配置", 140, 64, 134, 30, FluentButtonStyle.Outline);
            btnImportLegacy.Font = Theme.FontSmall();
            btnImportLegacy.Enabled = ToolboxConfig.HasLegacyEcoConfig();
            btnImportLegacy.Click += delegate { if (RequestImportLegacyConfig != null) RequestImportLegacyConfig(); };
            cardMeta.Controls.Add(btnImportLegacy);

            bool isAdmin = ElevationHelper.IsAdministrator();
            FluentButton btnElevate = FluentFactory.Button(
                isAdmin ? "当前已是管理员模式" : "以管理员身份重启",
                282, 64, 146, 30,
                isAdmin ? FluentButtonStyle.SoftSuccess : FluentButtonStyle.PrimaryBlue);
            btnElevate.Font = Theme.FontSmall();
            btnElevate.Enabled = !isAdmin;
            btnElevate.Click += delegate { if (RequestElevate != null) RequestElevate(); };
            cardMeta.Controls.Add(btnElevate);

            _lblStatusTip = new Label();
            _lblStatusTip.Text = "嘉豪工具箱 v1.0.0 · 原生 .NET 4.8 (x64) 单文件零依赖 · 配置存放在 AppData";
            _lblStatusTip.Font = new Font("Microsoft YaHei UI", 8F);
            _lblStatusTip.ForeColor = Theme.TextMuted;
            _lblStatusTip.BackColor = Color.White;
            _lblStatusTip.Location = new Point(18, 112);
            _lblStatusTip.Size = new Size(566, 22);
            cardMeta.Controls.Add(_lblStatusTip);
        }

        public void SyncFromConfig()
        {
            _loadingUi = true;
            try
            {
                _chkAutostart.Checked = AutostartManager.IsAutostartEnabled();
                _chkMinToTray.Checked = _config.MinimizeToTrayOnClose;
                _chkAutoStartWatcher.Checked = _config.AutoStartWatcher;
                _cmbPriority.SelectedIndex = string.Equals(_config.PriorityClass, "below_normal", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                int poll = _config.PollIntervalSeconds;
                if (poll < 2) poll = 4;
                if (poll > 30) poll = 30;
                _numPollSeconds.Value = poll;
                _chkFgExempt.Checked = _config.EnableForegroundExemption;
                _chkIdleTrim.Checked = _config.EnableIdleMemoryTrim;
            }
            finally
            {
                _loadingUi = false;
            }
        }

        private void OnAutostartChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            string detail;
            bool ok = AutostartManager.SetAutostart(_chkAutostart.Checked, out detail);
            _lblStatusTip.Text = detail;
            _lblStatusTip.ForeColor = ok ? Theme.GreenText : Theme.RedText;
            if (!ok)
            {
                _loadingUi = true;
                _chkAutostart.Checked = AutostartManager.IsAutostartEnabled();
                _loadingUi = false;
            }
        }

        private void OnSettingModified(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            _config.MinimizeToTrayOnClose = _chkMinToTray.Checked;
            _config.AutoStartWatcher = _chkAutoStartWatcher.Checked;
            _config.PriorityClass = _cmbPriority.SelectedIndex == 1 ? "below_normal" : "idle";
            _config.PollIntervalSeconds = (int)_numPollSeconds.Value;
            _config.EnableForegroundExemption = _chkFgExempt.Checked;
            _config.EnableIdleMemoryTrim = _chkIdleTrim.Checked;
            _config.Save();

            _lblStatusTip.Text = "设置已自动保存至 %APPDATA%\\JiahaoToolbox\\config.json。";
            _lblStatusTip.ForeColor = Theme.GreenText;

            if (SettingsChanged != null) SettingsChanged();
        }
    }
}
